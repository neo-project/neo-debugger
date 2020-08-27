using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo;
using Neo.IO;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.BlockchainToolkit.Persistence;
using Neo.SmartContract;
using Neo.SmartContract.Manifest;
using Neo.VM;
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
            var traceFilePath = config["trace-file"].Value<string>();
            if (traceFilePath == null)
            {
                throw new Exception("trace-file configuration not specified");
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
            foreach (var (path, storages) in ParseContracts(config))
            {
                var contract = LoadContract(path);

                var manifestPath = Path.ChangeExtension(path, ".manifest.json");
                var manifest = ContractManifest.Parse(File.ReadAllBytes(manifestPath));

                var id = AddContract(store, contract, manifest);
                AddStorage(store, id, storages);
            }

            var launchContract = LoadContract(config["program"].Value<string>());
            var invokeScript = await CreateLaunchScript(launchContract.ScriptHash, config);
            // var signer = Neo.Wallets.Helper.ToScriptHash("Nc2TJmEh7oM2wrXKdAQH5gHpy8HnyztcME");

            var tx = new Transaction
            {
                Version = 0,
                Nonce = (uint)new Random().Next(),
                Script = invokeScript,
                Signers = new[] { new Signer() { Account = signer, Scopes = WitnessScope.Global } },
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

        private static async Task<Neo.VM.Script> CreateLaunchScript(UInt160 scriptHash, Dictionary<string, JToken> config)
        {
            if (config.TryGetValue("invoke-file", out var invokeFile))
            {
                return await ContractParameterParser.LoadInvocationScript(invokeFile.Value<string>());
            }

            var operation = config.TryGetValue("operation", out var op)
                ? op.Value<string>()
                : throw new InvalidDataException("missing operation config");
            var args = config.TryGetValue("args", out var a)
                ? ContractParameterParser.ParseParams(a).ToArray()
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

        private static (TriggerType trigger, WitnessChecker witnessChecker)
            CreateRuntime(Dictionary<string, JToken> config)
        {
            if (config.TryGetValue("runtime", out var token))
            {
                var trigger = "verification".Equals(token.Value<string>("trigger"), StringComparison.InvariantCultureIgnoreCase)
                    ? TriggerType.Verification : TriggerType.Application;

                var witnesses = token["witnesses"];
                if (witnesses?.Type == JTokenType.Object)
                {
                    var checkResult = witnesses.Value<bool>("check-result");
                    var witnessChecker = new WitnessChecker(checkResult);
                    return (trigger, witnessChecker);
                }
                else if (witnesses?.Type == JTokenType.Array)
                {
                    var witnessChecker = new WitnessChecker(witnesses.Select(ParseWitness));
                    return (trigger, witnessChecker);
                }

                return (trigger, WitnessChecker.Default);
            }

            return (TriggerType.Application, WitnessChecker.Default);

            static UInt160 ParseWitness(JToken json)
            {
                var witness = json.Value<string>();
                if (witness.StartsWith("@N"))
                {
                    return Neo.Wallets.Helper.ToScriptHash(witness.Substring(1));
                }

                throw new Exception($"invalid witness \"{witness}\"");
            }
        }

        private static NefFile LoadContract(string path)
        {
            using var stream = File.OpenRead(path);
            using var reader = new BinaryReader(stream, Encoding.UTF8, false);
            return reader.ReadSerializable<NefFile>();
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
                var arg = ContractParameterParser.ParseStringParam(token?.Value<string>() ?? string.Empty);

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
