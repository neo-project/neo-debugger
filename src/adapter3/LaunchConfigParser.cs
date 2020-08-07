using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo;
using Neo.Cryptography.ECC;
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

namespace NeoDebug.Neo3
{
    static class LaunchConfigParser
    {
        public static DebugSession CreateDebugSession(LaunchArguments launchArguments, Action<DebugEvent> sendEvent, bool trace, DebugView defaultDebugView)
        {
            var config = launchArguments.ConfigurationProperties;
            var sourceFileMap = ParseSourceFileMap(config);
            var returnTypes = ParseReturnTypes(config).ToList();

            var (trigger, witnessChecker) = ParseRuntime(config);
            if (trigger != TriggerType.Application)
            {
                throw new Exception($"Trigger Type {trigger} not supported");
            }

            var debugInfoList = new List<DebugInfo>();
            var (launchContract, launchManifest, launchDebugInfo) = LoadContract(config["program"].Value<string>(), sourceFileMap);
            debugInfoList.Add(launchDebugInfo);

            IStore store = CreateBlockchainStorage(config);
            var launchId = AddContract(store, launchContract, launchManifest);
            AddStorage(store, launchId, ParseStorage(config));

            foreach (var (path, storages) in ParseContracts(config))
            {
                var (contract, manifest, debugInfo) = LoadContract(path, sourceFileMap);

                var id = AddContract(store, contract, manifest);
                AddStorage(store, id, storages);
                debugInfoList.Add(debugInfo);
            }

            var invokeScript = CreateLaunchScript(launchContract.ScriptHash, config);

            var tx = new Transaction
            {
                Version = 0,
                Nonce = (uint)(new Random()).Next(),
                Script = invokeScript,
                Signers = new[] { new Signer() { Account = UInt160.Zero } },
                ValidUntilBlock = Transaction.MaxValidUntilBlockIncrement,
                Attributes = Array.Empty<TransactionAttribute>(),
                Witnesses = Array.Empty<Witness>()
            };

            var engine = new DebugApplicationEngine(tx, new SnapshotView(store), witnessChecker);
            engine.LoadScript(invokeScript);

            return new DebugSession(engine, debugInfoList, returnTypes, sendEvent, defaultDebugView);

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

            static (NefFile contract, ContractManifest manifest, DebugInfo debugInfo)
                LoadContract(string contractPath, IReadOnlyDictionary<string, string> sourceFileMap)
            {
                var manifestPath = Path.ChangeExtension(contractPath, ".manifest.json");
                var manifest = ContractManifest.Parse(File.ReadAllBytes(manifestPath));

                using var stream = File.OpenRead(contractPath);
                using var reader = new BinaryReader(stream, Encoding.UTF8, false);
                var nefFile = reader.ReadSerializable<NefFile>();

                var debugInfo = DebugInfoParser.Load(contractPath, sourceFileMap);

                return (nefFile, manifest, debugInfo);
            }
        }

        static byte[] CreateLaunchScript(UInt160 scriptHash, Dictionary<string, JToken> config)
        {
            var operation = config.TryGetValue("operation", out var op)
                ? op.Value<string>() : throw new InvalidDataException("missing operation config");

            using var builder = new ScriptBuilder();
            builder.EmitAppCall(scriptHash, operation, ParseArguments(config).ToArray());
            return builder.ToArray();
        }

        static IStore CreateBlockchainStorage(Dictionary<string, JToken> config)
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

        static IEnumerable<(string contractPath, IEnumerable<(byte[] key, StorageItem item)> storages)> ParseContracts(Dictionary<string, JToken> config)
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

        static IEnumerable<string> ParseReturnTypes(Dictionary<string, JToken> config)
        {
            if (config.TryGetValue("return-types", out var returnTypes))
            {
                foreach (var returnType in returnTypes)
                {
                    yield return VariableManager.CastOperations[returnType.Value<string>()];
                }
            }
        }

        static (TriggerType trigger, WitnessChecker witnessChecker) ParseRuntime(Dictionary<string, JToken> config)
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

        static IEnumerable<(byte[] key, StorageItem item)> ParseStorage(Dictionary<string, JToken> config)
        {
            return config.TryGetValue("storage", out var token)
                ? ParseStorage(token)
                : Enumerable.Empty<(byte[], StorageItem)>();
        }

        static IEnumerable<(byte[] key, StorageItem item)> ParseStorage(JToken? token)
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

        static IReadOnlyDictionary<string, string> ParseSourceFileMap(Dictionary<string, JToken> config)
        {
            if (config.TryGetValue("sourceFileMap", out var token)
                && token.Type == JTokenType.Object)
            {
                var json = (IEnumerable<KeyValuePair<string, JToken?>>)token;
                return json.ToDictionary(kvp => kvp.Key, kvp => kvp.Value?.Value<string>() ?? string.Empty);
            }

            return ImmutableDictionary<string, string>.Empty;
        }

        static IEnumerable<ContractParameter> ParseArguments(Dictionary<string, JToken> config)
        {
            return config.TryGetValue("args", out var args)
                ? ContractParameterParser.ParseParams(args)
                : Enumerable.Empty<ContractParameter>();
        }
    }
}
