using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo;
using Neo.BlockchainToolkit;
using Neo.BlockchainToolkit.Persistence;
using Neo.Cryptography.ECC;
using Neo.IO;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.SmartContract;
using Neo.SmartContract.Manifest;
using Neo.VM;
using Neo.Wallets;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Nito.Disposables;

using Script = Neo.VM.Script;

namespace NeoDebug.Neo3
{
    using Invocation = OneOf.OneOf<LaunchConfigParser.InvokeFileInvocation, LaunchConfigParser.OracleResponseInvocation, LaunchConfigParser.LaunchInvocation>;
    using Storages = IEnumerable<(byte[] key, StorageItem item)>;

    internal static partial class LaunchConfigParser
    {
        readonly static Lazy<ContractParameterParser> contractParameterParser = new Lazy<ContractParameterParser>(() => new ContractParameterParser());

        public static async Task<IDebugSession> CreateDebugSessionAsync(LaunchArguments launchArguments, Action<DebugEvent> sendEvent, DebugView defaultDebugView)
        {
            var config = launchArguments.ConfigurationProperties;
            var sourceFileMap = ParseSourceFileMap(config);
            var returnTypes = ParseReturnTypes(config).ToList();
            var debugInfoList = await LoadDebugInfosAsync(config, sourceFileMap).ToListAsync().ConfigureAwait(false);
            var engine = await CreateEngineAsync(config).ConfigureAwait(false);

            return new DebugSession(engine, debugInfoList, returnTypes, sendEvent, defaultDebugView);
        }

        private static async Task<IApplicationEngine> CreateEngineAsync(Dictionary<string, JToken> config)
        {
            if (config.TryGetValue("invocation", out var json))
            {
                if (json["traceFile"] != null)
                {
                    return CreateTraceEngine(json.Value<string>("traceFile"), config);
                }

                if (TryGetInvocation(json, out var invocation))
                {
                    return await CreateDebugEngineAsync(invocation, config).ConfigureAwait(false);
                }
            }

            throw new JsonException("invalid invocation config");

            static bool TryGetInvocation(JToken json, out Invocation result)
            {
                if (json["invokeFile"] != null)
                {
                    result = new InvokeFileInvocation(json.Value<string>("invokeFile"));
                    return true;
                }

                if (OracleResponseInvocation.TryFromJson(json, out var oracleInvocation))
                {
                    result = oracleInvocation;
                    return true;
                }

                if (LaunchInvocation.TryFromJson(json, out var launchInvocation))
                {
                    result = launchInvocation;
                    return true;
                }

                result = default;
                return false;
            }
        }

        private static IApplicationEngine CreateTraceEngine(string traceFilePath, Dictionary<string, JToken> config)
        {
            var contracts = ParseStoredContracts(config).Select(t => LoadContract(t.contractPath));
            return new TraceApplicationEngine(traceFilePath, contracts);
        }

        private static async Task<IApplicationEngine> CreateDebugEngineAsync(Invocation invocation, Dictionary<string, JToken> config)
        {
            var (trigger, witnessChecker) = ParseRuntime(config);
            if (trigger != TriggerType.Application)
            {
                throw new Exception($"Trigger Type {trigger} not supported");
            }

            // CreateBlockchainStorage will call ProtocolSettings.Initialize for checkpoint based storage
            IStore store = CreateBlockchainStorage(config);
            EnsureNativeContractsDeployed(store);

            var launchContractPath = config["program"].Value<string>() ?? throw new Exception("missing program config property");
            var launchContract = LoadContract(launchContractPath);
            await AddContractStorageAsync(store, launchContract, launchContractPath, ParseStorage(config)).ConfigureAwait(false);

            foreach (var (path, storages) in ParseStoredContracts(config))
            {
                var contract = LoadContract(path);
                await AddContractStorageAsync(store, contract, path, storages).ConfigureAwait(false);
            }

            // ParseSigners access ProtocolSettings.Default so it needs to be after CreateBlockchainStorage
            var signers = ParseSigners(config).ToArray();
            var invokeScript = await CreateInvokeScriptAsync(invocation, launchContract.ScriptHash).ConfigureAwait(false);

            var tx = new Transaction
            {
                Version = 0,
                Nonce = (uint)new Random().Next(),
                Script = invokeScript,
                Signers = signers,
                ValidUntilBlock = Transaction.MaxValidUntilBlockIncrement,
                Attributes = GetTransactionAttributes(invocation, store, launchContract.ScriptHash),
                Witnesses = Array.Empty<Witness>()
            };

            var engine = new DebugApplicationEngine(tx, new SnapshotView(store), witnessChecker);
            engine.LoadScript(invokeScript);
            return engine;

            static async Task AddContractStorageAsync(IStore store, NefFile contract, string path, Storages storages)
            {
                var manifestBytes = await File.ReadAllBytesAsync(Path.ChangeExtension(path, ".manifest.json")).ConfigureAwait(false);
                var manifest = ContractManifest.Parse(manifestBytes);

                var id = AddContract(store, contract, manifest);

                using var snapshotView = new SnapshotView(store);
                foreach (var (key, item) in storages)
                {
                    var storageKey = new StorageKey()
                    {
                        Id = id,
                        Key = key
                    };
                    snapshotView.Storages.Add(storageKey, item);
                }
                snapshotView.Commit();
            }

            static int AddContract(IStore store, NefFile contract, ContractManifest manifest)
            {
                using var snapshotView = new SnapshotView(store);
                var contractState = snapshotView.Contracts.TryGet(contract.ScriptHash);
                if (contractState != null)
                {
                    return contractState.Id;
                }

                contractState = new ContractState
                {
                    Id = snapshotView.ContractId.GetAndChange().NextId++,
                    Script = contract.Script,
                    Manifest = manifest
                };
                snapshotView.Contracts.Add(contract.ScriptHash, contractState);
                snapshotView.Commit();

                return contractState.Id;
            }
        }

        private static NefFile LoadContract(string path)
        {
            using var stream = File.OpenRead(path);
            using var reader = new BinaryReader(stream, Encoding.UTF8, false);
            return reader.ReadSerializable<NefFile>();
        }

        private static TransactionAttribute[] GetTransactionAttributes(Invocation invocation, IStore store, UInt160 contractHash)
        {
            return invocation.Match(
                invoke => Array.Empty<TransactionAttribute>(),
                oracle => GetTransactionAttributes(oracle, store, contractHash),
                launch => Array.Empty<TransactionAttribute>());
        }

        private static TransactionAttribute[] GetTransactionAttributes(OracleResponseInvocation invocation, IStore store, UInt160 contractHash)
        {
            var userData = invocation.UserData == null
                ? Neo.VM.Types.Null.Null
                : contractParameterParser.Value.ParseParameter(invocation.UserData).ToStackItem();

            using var snapshotView = new SnapshotView(store);

            // the following logic to create the OracleRequest record that drives the response process
            // is adapted from OracleContract.Request
            const byte Prefix_RequestId = 9;
            const byte Prefix_Request = 7;
            const int MaxUserDataLength = 512;

            StorageItem item_id = snapshotView.Storages.GetAndChange(CreateStorageKey(Prefix_RequestId));
            ulong id = BitConverter.ToUInt64(item_id.Value) + 1;
            item_id.Value = BitConverter.GetBytes(id);

            snapshotView.Storages.Add(CreateStorageKey(Prefix_Request).Add(item_id.Value), new StorageItem(
                new Neo.SmartContract.Native.Oracle.OracleRequest
                {
                    OriginalTxid = UInt256.Zero,
                    GasForResponse = invocation.GasForResponse,
                    Url = invocation.Url,
                    Filter = invocation.Filter,
                    CallbackContract = contractHash,
                    CallbackMethod = invocation.Callback,
                    UserData = BinarySerializer.Serialize(userData, MaxUserDataLength)
                }));

            snapshotView.Commit();

            var response = new OracleResponse
            {
                Code = invocation.Code,
                Id = id,
                Result = Neo.Utility.StrictUTF8.GetBytes(Filter(invocation.Result, invocation.Filter))
            };

            return new TransactionAttribute[] { response };

            static Neo.SmartContract.Native.KeyBuilder CreateStorageKey(byte prefix)
            {
                // TODO: use of KeyBuilder depends on https://github.com/neo-project/neo/pull/2099
                const int Oracle_ContractId = -4;
                return new Neo.SmartContract.Native.KeyBuilder(Oracle_ContractId, prefix);
            }

            static string Filter(JToken json, string filterArgs)
            {
                if (string.IsNullOrEmpty(filterArgs))
                    return json.ToString();

                JArray afterObjects = new JArray(json.SelectTokens(filterArgs, true));
                return afterObjects.ToString();
            }
        }

        private static Task<Script> CreateInvokeScriptAsync(Invocation invocation, UInt160 scriptHash)
        {
            return invocation.Match<Task<Script>>(
                invoke =>
                {
                    return contractParameterParser.Value
                        .LoadInvocationScriptAsync(invoke.Path);
                },
                oracle => Task.FromResult<Script>(OracleResponse.FixedScript),
                launch =>
                {
                    var args = contractParameterParser.Value
                        .ParseParameters(launch.Args).ToArray();
                    using var builder = new ScriptBuilder();
                    builder.EmitAppCall(scriptHash, launch.Operation, args);
                    return Task.FromResult<Script>(builder.ToArray());
                });
        }

        private static IStore CreateBlockchainStorage(Dictionary<string, JToken> config)
        {
            if (config.TryGetValue("checkpoint", out var checkpoint))
            {
                string checkpointTempPath;
                do
                {
                    checkpointTempPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
                }
                while (Directory.Exists(checkpointTempPath));

                var cleanup = AnonymousDisposable.Create(() =>
                {
                    if (Directory.Exists(checkpointTempPath))
                    {
                        Directory.Delete(checkpointTempPath);
                    }
                });

                var magic = RocksDbStore.RestoreCheckpoint(checkpoint.Value<string>(), checkpointTempPath);
                var settings = new[] { KeyValuePair.Create("ProtocolConfiguration:Magic", $"{magic}") };
                var protocolConfig = new ConfigurationBuilder()
                    .AddInMemoryCollection(settings)
                    .Build();

                if (!ProtocolSettings.Initialize(protocolConfig))
                {
                    throw new Exception("could not initialize protocol settings");
                }

                return new CheckpointStore(
                    RocksDbStore.OpenReadOnly(checkpointTempPath),
                    cleanup);
            }
            else
            {
                return new MemoryStore();
            }
        }

        private static void EnsureNativeContractsDeployed(IStore store)
        {
            using var snapshot = new SnapshotView(store);
            if (snapshot.Contracts.Find().Any(c => c.Value.Id < 0)) return;

            using var sb = new Neo.VM.ScriptBuilder();
            sb.EmitSysCall(ApplicationEngine.Neo_Native_Deploy);

            using var engine = ApplicationEngine.Run(sb.ToArray(), snapshot, persistingBlock: new Block());
            if (engine.State != VMState.HALT) throw new Exception("Neo_Native_Deploy failed");
            snapshot.Commit();
        }

        private static (TriggerType trigger, Func<byte[], bool>? witnessChecker) ParseRuntime(Dictionary<string, JToken> config)
        {
            var hasCheckpoint = config.TryGetValue("checkpoint", out _);

            if (config.TryGetValue("runtime", out var token))
            {
                var trigger = "verification".Equals(token.Value<string>("trigger"), StringComparison.InvariantCultureIgnoreCase)
                    ? TriggerType.Verification : TriggerType.Application;

                var checkWitness = token["check-witness"];
                if (checkWitness?.Type == JTokenType.Boolean)
                {
                    return (trigger, _ => checkWitness.Value<bool>());
                }
                else if (checkWitness?.Type == JTokenType.Array)
                {
                    var witnesses = checkWitness.Select(t => ParseAddress(t.Value<string>())).ToImmutableSortedSet();
                    return (trigger, hashOrPubkey => CheckWitness(hashOrPubkey, witnesses));
                }
                else if (checkWitness?.Type == JTokenType.String)
                {
                    if (checkWitness.Value<string>() != "checkpoint")
                    {
                        throw new Exception($"invalid check-witness value \"{checkWitness.Value<string>()}\"");
                    }

                    if (!hasCheckpoint)
                    {
                        throw new Exception("invalid launch config - checkpoint not specified");
                    }
                }

                return (trigger, DefaultWitnessChecker());
            }

            return (TriggerType.Application, DefaultWitnessChecker());

            Func<byte[], bool>? DefaultWitnessChecker() => hasCheckpoint ? (Func<byte[], bool>?)null : _ => true;

            static bool CheckWitness(byte[] hashOrPubkey, ImmutableSortedSet<UInt160> witnesses)
            {
                // hashOrPubkey parsing logic copied from ApplicationEngine.CheckWitness
                var hash = hashOrPubkey.Length switch
                {
                    20 => new UInt160(hashOrPubkey),
                    33 => Contract.CreateSignatureRedeemScript(ECPoint.DecodePoint(hashOrPubkey, ECCurve.Secp256r1)).ToScriptHash(),
                    _ => throw new ArgumentException()
                };
                return witnesses.Contains(hash);
            }
        }

        private static IEnumerable<Signer> ParseSigners(Dictionary<string, JToken> config)
        {
            if (config.TryGetValue("signers", out var signers))
            {
                foreach (var signer in signers)
                {
                    if (signer.Type == JTokenType.String)
                    {
                        var account = ParseAddress(signer.Value<string>());
                        yield return new Signer { Account = account, Scopes = WitnessScope.CalledByEntry };
                    }
                    else if (signer.Type == JTokenType.Object)
                    {
                        var account = ParseAddress(signer.Value<string>("account"));
                        var textScopes = signer.Value<string>("scopes");
                        var scopes = textScopes == null
                            ? WitnessScope.CalledByEntry
                            : (WitnessScope)Enum.Parse(typeof(WitnessScope), textScopes);
                        var s = new Signer { Account = account, Scopes = scopes };
                    }
                }
            }
        }

        private static UInt160 ParseAddress(string text)
        {
            if (text[0] == '@')
            {
                return text[1..].ToScriptHash();
            }

            return text.ToScriptHash();
        }

        private static async IAsyncEnumerable<DebugInfo> LoadDebugInfosAsync(Dictionary<string, JToken> config, IReadOnlyDictionary<string, string> sourceFileMap)
        {
            yield return await DebugInfoParser.Load(config["program"].Value<string>(), sourceFileMap).ConfigureAwait(false);

            foreach (var (contractPath, _) in ParseStoredContracts(config))
            {
                yield return await DebugInfoParser.Load(contractPath, sourceFileMap).ConfigureAwait(false);
            }
        }

        private static IEnumerable<(string contractPath, Storages storages)> ParseStoredContracts(Dictionary<string, JToken> config)
        {
            if (config.TryGetValue("stored-contracts", out var storedContracts))
            {
                foreach (var storedContract in storedContracts)
                {
                    if (storedContract.Type == JTokenType.String)
                    {
                        var path = storedContract.Value<string>();
                        var storages = Enumerable.Empty<(byte[], StorageItem)>();
                        yield return (path, storages);
                    }
                    else if (storedContract.Type == JTokenType.Object)
                    {
                        var path = storedContract.Value<string>("program");
                        var storages = ParseStorage(storedContract["storage"]);
                        yield return (path, storages);
                    }
                    else
                    {
                        throw new Exception("invalid stored-contract value");
                    }
                }
            }
        }

        private static IEnumerable<string> ParseReturnTypes(Dictionary<string, JToken> config)
        {
            if (config.TryGetValue("return-types", out var returnTypes))
            {
                foreach (var returnType in returnTypes)
                {
                    yield return VariableManager.CastOperations[returnType.Value<string>()];
                }
            }
        }

        private static Storages ParseStorage(Dictionary<string, JToken> config)
        {
            return config.TryGetValue("storage", out var token)
                ? ParseStorage(token)
                : Enumerable.Empty<(byte[], StorageItem)>();
        }

        private static Storages ParseStorage(JToken? token)
        {
            return token == null
                ? Enumerable.Empty<(byte[], StorageItem)>()
                : token.Select(t =>
                    {
                        var key = ConvertParameter(t["key"]);
                        var item = new StorageItem
                        {
                            Value = ConvertParameter(t["value"]),
                            IsConstant = t.Value<bool?>("constant") ?? false
                        };
                        return (key, item);
                    });

            static byte[] ConvertParameter(JToken? token)
            {
                var arg = contractParameterParser.Value.ParseParameter(token ?? JValue.CreateNull());
                return arg.ToStackItem().GetSpan().ToArray();
            }
        }

        private static IReadOnlyDictionary<string, string> ParseSourceFileMap(Dictionary<string, JToken> config)
        {
            if (config.TryGetValue("sourceFileMap", out var token)
                && token.Type == JTokenType.Object)
            {
                var json = (IEnumerable<KeyValuePair<string, JToken?>>)token;
                return json.ToDictionary(kvp => kvp.Key, kvp => kvp.Value?.Value<string>() ?? string.Empty);
            }

            return ImmutableDictionary<string, string>.Empty;
        }
    }
}
