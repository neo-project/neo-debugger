using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo;
using Neo.IO;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.BlockchainToolkit;
using Neo.BlockchainToolkit.Persistence;
using Neo.SmartContract;
using Neo.SmartContract.Manifest;
using Neo.VM;
using Neo.Wallets;
using Newtonsoft.Json.Linq;
using Nito.Disposables;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using NeoScript = Neo.VM.Script;
using Neo.Cryptography.ECC;

namespace NeoDebug.Neo3
{
    internal static class LaunchConfigParser
    {
        public static async Task<IDebugSession> CreateDebugSession(LaunchArguments launchArguments, Action<DebugEvent> sendEvent, bool trace, DebugView defaultDebugView)
        {
            var config = launchArguments.ConfigurationProperties;
            var sourceFileMap = ParseSourceFileMap(config);
            var returnTypes = ParseReturnTypes(config).ToList();
            var debugInfoList = await ParseDebugInfo(config, sourceFileMap).ToListAsync().ConfigureAwait(false);

            var engine = trace
                ? CreateTraceEngine(config)
                : await CreateDebugEngine(config);

            return new DebugSession(engine, debugInfoList, returnTypes, sendEvent, defaultDebugView);
        }

        private static IApplicationEngine CreateTraceEngine(Dictionary<string, JToken> config)
        {
            var traceFilePath = config["traceFile"].Value<string>();
            if (traceFilePath == null)
            {
                throw new Exception("traceFile configuration not specified");
            }

            var contracts = ParseContracts(config).Select(t => LoadContract(t.contractPath));

            return new TraceApplicationEngine(traceFilePath, contracts);
        }

        private static async Task<IApplicationEngine> CreateDebugEngine(Dictionary<string, JToken> config)
        {
            var (trigger, witnessChecker) = CreateRuntime(config);
            if (trigger != TriggerType.Application)
            {
                throw new Exception($"Trigger Type {trigger} not supported");
            }

            IStore store = CreateBlockchainStorage(config);
            EnsureNativeContractsDeployed(store);
            foreach (var (path, storages) in ParseContracts(config))
            {
                var contract = LoadContract(path);

                var manifestPath = Path.ChangeExtension(path, ".manifest.json");
                var manifestBytes = await File.ReadAllBytesAsync(manifestPath).ConfigureAwait(false);
                var manifest = ContractManifest.Parse(manifestBytes);

                var id = AddContract(store, contract, manifest);
                AddStorage(store, id, storages);
            }

            var launchContract = LoadContract(config["program"].Value<string>());
            var invokeScript = await CreateLaunchScript(launchContract.ScriptHash, config);
            var signers = ParseSigners(config).ToArray();

            var tx = new Transaction
            {
                Version = 0,
                Nonce = (uint)new Random().Next(),
                Script = invokeScript,
                Signers = signers,
                ValidUntilBlock = Transaction.MaxValidUntilBlockIncrement,
                Attributes = Array.Empty<TransactionAttribute>(),
                Witnesses = Array.Empty<Witness>()
            };

            var engine = new DebugApplicationEngine(tx, new SnapshotView(store), witnessChecker);
            engine.LoadScript(invokeScript);

            return engine;

            static void AddStorage(IStore store, int contractId, IEnumerable<(byte[] key, StorageItem item)> storages)
            {
                var snapshotView = new SnapshotView(store);
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

            static int AddContract(IStore store, NefFile contract, ContractManifest manifest)
            {
                var snapshotView = new SnapshotView(store);
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

        private static async Task<NeoScript> CreateLaunchScript(UInt160 scriptHash, Dictionary<string, JToken> config)
        {
            var parser = new ContractParameterParser();

            if (config.TryGetValue("invokeFile", out var invokeFile))
            {
                return await parser.LoadInvocationScriptAsync(invokeFile.Value<string>()).ConfigureAwait(false);
            }

            var operation = config.TryGetValue("operation", out var op)
                ? op.Value<string>()
                : throw new InvalidDataException("missing operation config");
            var args = config.TryGetValue("args", out var a)
                ? parser.ParseParameters(a).ToArray()
                : Array.Empty<ContractParameter>();

            using var builder = new ScriptBuilder();
            builder.EmitAppCall(scriptHash, operation, args);
            return builder.ToArray();
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
                if (!InitializeProtocolSettings(magic))
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

            static bool InitializeProtocolSettings(long magic)
            {
                IEnumerable<KeyValuePair<string, string>> settings()
                {
                    yield return new KeyValuePair<string, string>(
                        "ProtocolConfiguration:Magic", $"{magic}");
                }

                var config = new ConfigurationBuilder()
                    .AddInMemoryCollection(settings())
                    .Build();

                return ProtocolSettings.Initialize(config);
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

        private static (TriggerType trigger, Func<byte[], bool>? witnessChecker) CreateRuntime(Dictionary<string, JToken> config)
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

        private static UInt160 ParseAddress(string text)
        {
            if (text[0] == '@')
            {
                return text[1..].ToScriptHash();
            }

            return text.ToScriptHash();
        }

        private static NefFile LoadContract(string path)
        {
            using var stream = File.OpenRead(path);
            using var reader = new BinaryReader(stream, Encoding.UTF8, false);
            return reader.ReadSerializable<NefFile>();
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

        private static async IAsyncEnumerable<DebugInfo> ParseDebugInfo(Dictionary<string, JToken> config, IReadOnlyDictionary<string, string> sourceFileMap)
        {
            foreach (var (contractPath, _) in ParseContracts(config))
            {
                yield return await DebugInfoParser.Load(contractPath, sourceFileMap).ConfigureAwait(false);
            }
        }

        private static IEnumerable<(string contractPath, IEnumerable<(byte[] key, StorageItem item)> storages)>
            ParseContracts(Dictionary<string, JToken> config)
        {
            yield return (config["program"].Value<string>(), ParseStorage(config));

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

        private static IEnumerable<(byte[] key, StorageItem item)>
            ParseStorage(Dictionary<string, JToken> config)
        {
            return config.TryGetValue("storage", out var token)
                ? ParseStorage(token)
                : Enumerable.Empty<(byte[], StorageItem)>();
        }

        private static IEnumerable<(byte[] key, StorageItem item)>
            ParseStorage(JToken? token)
        {
            return token == null
                ? Enumerable.Empty<(byte[], StorageItem)>()
                : token.Select(t =>
                    {
                        var key = ConvertString(t["key"]);
                        var item = new StorageItem
                        {
                            Value = ConvertString(t["value"]),
                            IsConstant = t.Value<bool?>("constant") ?? false
                        };
                        return (key, item);
                    });

            static byte[] ConvertString(JToken? token)
            {
                var parser = new ContractParameterParser();
                var arg = parser.ParseStringParameter(token?.Value<string>() ?? string.Empty, string.Empty);

                return arg.Type switch
                {
                    ContractParameterType.Hash160 => ((UInt160)arg.Value).ToArray(),
                    ContractParameterType.Integer => ((BigInteger)arg.Value).ToByteArray(),
                    ContractParameterType.String => Encoding.UTF8.GetBytes((string)arg.Value),
                    _ => throw new InvalidDataException(),
                };
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
