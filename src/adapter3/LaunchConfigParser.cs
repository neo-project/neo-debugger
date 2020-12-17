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
using Neo.SmartContract.Native;
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

    partial class LaunchConfigParser
    {
        readonly Dictionary<string, JToken> config;
        readonly Dictionary<string, UInt160> contracts = new Dictionary<string, UInt160>();
        readonly Dictionary<string, UInt160> accounts = new Dictionary<string, UInt160>();
        readonly ContractParameterParser paramParser;

        public LaunchConfigParser(LaunchArguments launchArguments)
        {
            config = launchArguments.ConfigurationProperties;
            paramParser = new ContractParameterParser(accounts.TryGetValue, contracts.TryGetValue);
        }

        public static Task<IDebugSession> CreateDebugSessionAsync(LaunchArguments launchArguments, Action<DebugEvent> sendEvent, DebugView defaultDebugView)
        {
            var launchParser = new LaunchConfigParser(launchArguments);
            return launchParser.CreateDebugSessionAsync(sendEvent, defaultDebugView);
        }

        async Task<IDebugSession> CreateDebugSessionAsync(Action<DebugEvent> sendEvent, DebugView defaultDebugView)
        {
            var sourceFileMap = ParseSourceFileMap(config);
            var debugInfoList = await LoadDebugInfosAsync(sourceFileMap).ToListAsync().ConfigureAwait(false);
            var returnTypes = ParseReturnTypes(config).ToList();
            var engine = await CreateEngineAsync().ConfigureAwait(false);
            return new DebugSession(engine, debugInfoList, returnTypes, sendEvent, defaultDebugView);
        }

        async Task<IApplicationEngine> CreateEngineAsync()
        {
            if (config.TryGetValue("invocation", out var json))
            {
                if (json["traceFile"] != null)
                {
                    return CreateTraceEngine(json.Value<string>("traceFile"));
                }

                if (TryGetInvocation(json, out var invocation))
                {
                    return await CreateDebugEngineAsync(invocation).ConfigureAwait(false);
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

                if (OracleResponseInvocation.TryFromJson(json["oracleResponse"], out var oracleInvocation))
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

        IApplicationEngine CreateTraceEngine(string traceFilePath)
        {
            var contracts = new List<NefFile>();
            var launchContractPath = config["program"].Value<string>() ?? throw new Exception("missing program config property");
            var launchContract = LoadContract(launchContractPath);
            contracts.Add(launchContract);

            // TODO: load other contracts

            return new TraceApplicationEngine(traceFilePath, contracts);
        }

        async Task<IApplicationEngine> CreateDebugEngineAsync(Invocation invocation)
        {
            var (trigger, witnessChecker) = ParseRuntime(config);
            if (trigger != TriggerType.Application)
            {
                throw new Exception($"Trigger Type {trigger} not supported");
            }

            // CreateBlockchainStorage will call ProtocolSettings.Initialize for checkpoint based storage
            IStore store = CreateBlockchainStorage(config);
            // EnsureNativeContractsDeployed(store);

            var launchContractPath = config["program"].Value<string>() ?? throw new Exception("missing program config property");
            var launchContract = LoadContract(launchContractPath);
            var launchContractManifest = await LoadManifest(launchContractPath);
            var (lauchContractId, launchContractHash) = AddContract(store, launchContract, launchContractManifest);

            // TODO: load other contracts

            // Not sure supporting other contracts is a good idea anymore. Since there's no way to calcualte the 
            // contract id hash prior to deployment in Neo 3, I'm thinking the better approach would be to simply
            // deploy whatever contracts you want and take a snapshot rather than deploying multiple contracts 
            // during launch configuration.

            // cache the deployed contracts for use by parameter parser
            using (var snapshotView = new SnapshotView(store))
            {
                foreach (var contractState in NativeContract.Management.ListContracts(snapshotView))
                {
                    contracts.TryAdd(contractState.Manifest.Name, contractState.Hash);
                }
            }

            UpdateContractStorage(store, lauchContractId, ParseStorage());

            // TODO: load other contract storage (unless we cut this feature - see comment above)

            // ParseSigners access ProtocolSettings.Default so it needs to be after CreateBlockchainStorage
            var signers = ParseSigners(config).ToArray();
            var invokeScript = await CreateInvokeScriptAsync(invocation, launchContractHash).ConfigureAwait(false);

            var tx = new Transaction
            {
                Version = 0,
                Nonce = (uint)new Random().Next(),
                Script = invokeScript,
                Signers = signers,
                ValidUntilBlock = Transaction.MaxValidUntilBlockIncrement,
                Attributes = GetTransactionAttributes(invocation, store, launchContractHash),
                Witnesses = Array.Empty<Witness>()
            };

            var snapshot = new SnapshotView(store);
            snapshot.PersistingBlock = new Block();
            var engine = new DebugApplicationEngine(tx, snapshot, witnessChecker);
            engine.LoadScript(invokeScript);
            return engine;

            static (int id, UInt160 scriptHash) AddContract(IStore store, NefFile contract, ContractManifest manifest)
            {
                // TODO: Can we refactor NativeContract.Management to support contract add and update from the debugger
                const byte ManagementContract_PREFIX_CONTRACT = 8;

                using var snapshotView = new SnapshotView(store);

                // check to see if there's a contract with a name that matches the name in the manifest
                foreach (var contractState in NativeContract.Management.ListContracts(snapshotView))
                {
                    // do not update native contracts, even if names match
                    if (contractState.Id <= 0) continue;

                    if (string.Equals(contractState.Manifest.Name, manifest.Name))
                    {
                        // if the deployed script doesn't match the script parameter, overwrite the deployed script
                        if (contract.Script.ToScriptHash() != contractState.Script.ToScriptHash())
                        {
                            var key = new KeyBuilder(NativeContract.Management.Id, ManagementContract_PREFIX_CONTRACT).Add(contractState.Hash);
                            var cs = snapshotView.Storages.GetAndChange(key)?.GetInteroperable<ContractState>();
                            if (cs == null) throw new Exception();

                            cs.Script = contract.Script;
                            cs.Manifest = manifest;
                            snapshotView.Commit();
                        }

                        return (contractState.Id, contractState.Hash);
                    }
                }

                {
                    // if no existing contract with matching manifest.Name is found, 
                    // add the provided contract to storage

                    // logic from ManagementContract.GetNextAvailableId
                    const byte ManagementContract_PREFIX_NEXT_ID = 15;
                    var item = snapshotView.Storages.GetAndChange(
                        new KeyBuilder(NativeContract.Management.Id, ManagementContract_PREFIX_NEXT_ID),
                        () => new StorageItem(1));
                    var id = (int)(System.Numerics.BigInteger)item;
                    item.Add(1);

                    var hash = Neo.SmartContract.Helper.GetContractHash(UInt160.Zero, contract.Script);
                    var contractState = new ContractState
                    {
                        Id = id,
                        UpdateCounter = 0,
                        Script = contract.Script,
                        Hash = hash,
                        Manifest = manifest
                    };

                    var key = new KeyBuilder(NativeContract.Management.Id, ManagementContract_PREFIX_CONTRACT)
                        .Add(hash);
                    snapshotView.Storages.Add(key, new StorageItem(contractState));
                    snapshotView.Commit();

                    return (contractState.Id, hash);
                }
            }

            static void UpdateContractStorage(IStore store, int contractId, Storages storages)
            {
                using var snapshotView = new SnapshotView(store);
                foreach (var (key, item) in storages)
                {
                    var storageKey = new StorageKey()
                    {
                        Id = contractId,
                        Key = key
                    };
                    snapshotView.Storages.Add(storageKey, item);
                }
                snapshotView.Commit();
            }
        }

        private static NefFile LoadContract(string path)
        {
            using var stream = File.OpenRead(path);
            using var reader = new BinaryReader(stream, Encoding.UTF8, false);
            return reader.ReadSerializable<NefFile>();
        }

        private static async Task<ContractManifest> LoadManifest(string contractPath)
        {
            var manifestBytes = await File.ReadAllBytesAsync(Path.ChangeExtension(contractPath, ".manifest.json")).ConfigureAwait(false);
            return ContractManifest.Parse(manifestBytes);
        }

        private static TransactionAttribute[] GetTransactionAttributes(Invocation invocation, IStore store, UInt160 contractHash)
        {
            return invocation.Match(
                invoke => Array.Empty<TransactionAttribute>(),
                oracle => GetTransactionAttributes(oracle, store, contractHash),
                launch => Array.Empty<TransactionAttribute>());
        }

        TransactionAttribute[] GetTransactionAttributes(OracleResponseInvocation invocation, IStore store, UInt160 contractHash)
        {
            var userData = invocation.UserData == null
                ? Neo.VM.Types.Null.Null
                : paramParser.ParseParameter(invocation.UserData).ToStackItem();

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
                new Neo.SmartContract.Native.OracleRequest
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

            static Neo.SmartContract.KeyBuilder CreateStorageKey(byte prefix)
            {
                const int Oracle_ContractId = -4;
                return new Neo.SmartContract.KeyBuilder(Oracle_ContractId, prefix);
            }

            static string Filter(JToken json, string filterArgs)
            {
                if (string.IsNullOrEmpty(filterArgs))
                    return json.ToString();

                JArray afterObjects = new JArray(json.SelectTokens(filterArgs, true));
                return afterObjects.ToString();
            }
        }

        Task<Script> CreateInvokeScriptAsync(Invocation invocation, UInt160 scriptHash)
        {
            return invocation.Match<Task<Script>>(
                invoke =>
                {
                    return paramParser.LoadInvocationScriptAsync(invoke.Path);
                },
                oracle => Task.FromResult<Script>(OracleResponse.FixedScript),
                launch =>
                {
                    if (launch.Contract.Length > 0
                        && paramParser.TryLoadScriptHash(launch.Contract, out var hash))
                    {
                        scriptHash = hash;
                    }

                    var args = paramParser.ParseParameters(launch.Args).ToArray();
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

        // private static void EnsureNativeContractsDeployed(IStore store)
        // {
        //     using var snapshot = new SnapshotView(store);
        //     if (snapshot.Contracts.Find().Any(c => c.Value.Id < 0)) return;

        //     using var sb = new Neo.VM.ScriptBuilder();
        //     sb.EmitSysCall(ApplicationEngine.Neo_Native_Deploy);

        //     using var engine = ApplicationEngine.Run(sb.ToArray(), snapshot, persistingBlock: new Block());
        //     if (engine.State != VMState.HALT) throw new Exception("Neo_Native_Deploy failed");
        //     snapshot.Commit();
        // }

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
                        yield return new Signer { Account = account, Scopes = scopes };
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

        async IAsyncEnumerable<DebugInfo> LoadDebugInfosAsync(IReadOnlyDictionary<string, string> sourceFileMap)
        {
            yield return await DebugInfoParser.Load(config["program"].Value<string>(), sourceFileMap).ConfigureAwait(false);

            foreach (var (contractPath, _) in ParseStoredContracts())
            {
                yield return await DebugInfoParser.Load(contractPath, sourceFileMap).ConfigureAwait(false);
            }
        }

        IEnumerable<(string contractPath, Storages storages)> ParseStoredContracts()
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

        Storages ParseStorage()
        {
            return config.TryGetValue("storage", out var token)
                ? ParseStorage(token)
                : Enumerable.Empty<(byte[], StorageItem)>();
        }

        Storages ParseStorage(JToken? token)
        {
            return token == null
                ? Enumerable.Empty<(byte[], StorageItem)>()
                : token.Select(t =>
                    {
                        var key = ConvertParameter(t["key"], paramParser);
                        var item = new StorageItem
                        {
                            Value = ConvertParameter(t["value"], paramParser),
                            IsConstant = t.Value<bool?>("constant") ?? false
                        };
                        return (key, item);
                    });

            static byte[] ConvertParameter(JToken? token, ContractParameterParser paramParser)
            {
                var arg = paramParser.ParseParameter(token ?? JValue.CreateNull());
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
