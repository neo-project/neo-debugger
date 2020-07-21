using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo;
using Neo.Cryptography.ECC;
using Neo.IO;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.Seattle.Persistence;
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
        public static DebugSession CreateDebugSession(LaunchArguments launchArguments, Action<DebugEvent> sendEvent, DebugView defaultDebugView)
        {
            var config = launchArguments.ConfigurationProperties;
            var sourceFileMap = ParseSourceFileMap(config);
            var (trigger, witnessChecker) = ParseRuntime(config);
            if (trigger != TriggerType.Application)
            {
                throw new Exception($"Trigger Type {trigger} not supported");
            }

            var (launchContract, _) = LoadContract(config["program"].Value<string>());

            IStore store = CreateBlockchainStorage(config);

            foreach (var (path, storages) in ParseContracts(config))
            {
                var (contract, manifest) = LoadContract(path);
                var debugInfo = DebugInfoParser.Load(path, sourceFileMap);

                var id = AddContract(store, contract, manifest);
                AddStorage(store, id, storages);
                debugInfo.Put(store);
            }

            var invokeScript = CreateLaunchScript(launchContract.ScriptHash, config);

            var tx = new Transaction
            {
                Version = 0,
                Nonce = (uint)(new Random()).Next(),
                Script = invokeScript,
                Signers = new Signer[] { UInt160.Zero },
                ValidUntilBlock = Transaction.MaxValidUntilBlockIncrement,
                Attributes = Array.Empty<TransactionAttribute>(),
                Witnesses = Array.Empty<Witness>()
            };

            var engine = new DebugApplicationEngine(tx, new SnapshotView(store), witnessChecker);
            engine.LoadScript(invokeScript);

            var returnTypes = ParseReturnTypes(config).ToList();
            return new DebugSession(engine, store, returnTypes, sendEvent, defaultDebugView);

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

                contractState = new Neo.Ledger.ContractState
                {
                    Id = snapshotView.ContractId.GetAndChange().NextId++,
                    Script = contract.Script,
                    Manifest = manifest
                };
                snapshotView.Contracts.Add(contract.ScriptHash, contractState);

                snapshotView.Commit();

                return contractState.Id;
            }

            static (NefFile contract, ContractManifest manifest) LoadContract(string contractPath)
            {
                var manifestPath = System.IO.Path.ChangeExtension(contractPath, ".manifest.json");
                var manifest = ContractManifest.Parse(File.ReadAllBytes(manifestPath));

                using var stream = File.OpenRead(contractPath);
                using var reader = new BinaryReader(stream, Encoding.UTF8, false);
                var nefFile = reader.ReadSerializable<NefFile>();

                return (nefFile, manifest);
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

                RocksDbStore.RestoreCheckpoint(checkpoint.Value<string>(), checkpointTempPath);
                return new CheckpointStore(
                    RocksDbStore.OpenReadOnly(checkpointTempPath),
                    cleanup); 
            }
            else
            {
                return new MemoryStore();
            }
        }

        static IEnumerable<(string contractPath, IEnumerable<(byte[] key, StorageItem item)> storages)> ParseContracts(Dictionary<string, JToken> config)
        {
            var program = config["program"].Value<string>();
            var programStorages = ParseStorage(config);

            yield return (program, programStorages);

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
                var arg = ParseStringArg(token?.Value<string>() ?? string.Empty);

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
            if (config.TryGetValue("args", out var args))
            {
                if (args is JArray jArgs)
                {
                    for (int i = 0; i < jArgs.Count; i++)
                    {
                        yield return ParseArg(jArgs[i]);
                    }
                }
                else
                {
                    yield return ParseArg(args);
                }
            }
        }

        // TODO: DRY out ParseArgs between NeoExpress + NeoDebugger
        static ContractParameter ParseArg(JToken arg)
        {
            return arg.Type switch
            {
                JTokenType.String => ParseStringArg(arg.Value<string>()),
                JTokenType.Boolean => new ContractParameter()
                {
                    Type = ContractParameterType.Boolean,
                    Value = arg.Value<bool>()
                },
                JTokenType.Integer => new ContractParameter()
                {
                    Type = ContractParameterType.Integer,
                    Value = new System.Numerics.BigInteger(arg.Value<int>())
                },
                JTokenType.Array => new ContractParameter()
                {
                    Type = ContractParameterType.Array,
                    Value = ((JArray)arg).Select(ParseArg).ToList(),
                },
                JTokenType.Object => ParseObjectArg((JObject)arg),
                _ => throw new Exception()
            };
        }

        static ContractParameter ParseStringArg(string arg)
        {
            if (arg.StartsWith("@N"))
            {
                var hash = Neo.Wallets.Helper.ToScriptHash(arg.Substring(1));
                return new ContractParameter()
                {
                    Type = ContractParameterType.Hash160,
                    Value = hash
                };
            }

            if (arg.StartsWith("0x")
                && BigInteger.TryParse(arg.AsSpan().Slice(2), System.Globalization.NumberStyles.HexNumber, null, out var bigInteger))
            {
                return new ContractParameter()
                {
                    Type = ContractParameterType.Integer,
                    Value = bigInteger
                };
            }

            return new ContractParameter()
            {
                Type = ContractParameterType.String,
                Value = arg
            };
        }


        static ContractParameter ParseObjectArg(JObject arg)
        {
            var type = Enum.Parse<ContractParameterType>(arg.Value<string>("type"));
            object value = type switch
            {
                // TODO: support hex encoding such as hex for byte array and signature
                ContractParameterType.ByteArray => Convert.FromBase64String(arg.Value<string>("value")),
                ContractParameterType.Signature => Convert.FromBase64String(arg.Value<string>("value")),
                ContractParameterType.Boolean => arg.Value<bool>("value"),
                ContractParameterType.Integer => BigInteger.Parse(arg.Value<string>("value")),
                ContractParameterType.Hash160 => UInt160.Parse(arg.Value<string>("value")),
                ContractParameterType.Hash256 => UInt256.Parse(arg.Value<string>("value")),
                ContractParameterType.PublicKey => ECPoint.Parse(arg.Value<string>("value"), ECCurve.Secp256r1),
                ContractParameterType.String => arg.Value<string>("value"),
                ContractParameterType.Array => arg["value"].Select(ParseArg).ToArray(),
                ContractParameterType.Map => arg["value"].Select(ParseMapElement).ToArray(),
                _ => throw new ArgumentException(nameof(arg) + $" {type}"),
            };

            return new ContractParameter()
            {
                Type = type,
                Value = value,
            };

            static KeyValuePair<ContractParameter, ContractParameter> ParseMapElement(JToken json) => 
                KeyValuePair.Create(
                    ParseArg(json["key"] ?? throw new InvalidOperationException()), 
                    ParseArg(json["value"] ?? throw new InvalidOperationException()));
        }

    }
}
