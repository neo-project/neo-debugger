using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo;
using Neo.IO;
using Neo.Ledger;
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
            var program = config["program"].Value<string>();
            var (contract, manifest) = LoadContract(program);
            var debugInfo = DebugInfoParser.Load(program, sourceFileMap);

            IStore store = CreateBlockchainStorage(config);
            var id = AddContract(store, contract, manifest);
            AddStorage(store, ParseStorage(id, config));
            debugInfo.Put(store);

            var engine = new DebugApplicationEngine(new SnapshotView(store));
            var invokeScript = CreateLaunchScript(contract, config);
            engine.LoadScript(invokeScript);

            var returnTypes = ParseReturnTypes(config).ToList();
            return new DebugSession(engine, store, returnTypes, sendEvent, defaultDebugView);

            static void AddStorage(IStore store, IEnumerable<(StorageKey key, StorageItem item)> storages)
            {
                var snapshotView = new SnapshotView(store);
                foreach (var (key, item) in storages)
                {
                    snapshotView.Storages.Add(key, item);
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
                var nefFile = Neo.IO.Helper.ReadSerializable<NefFile>(reader);

                return (nefFile, manifest);
            }
        }

        static byte[] CreateLaunchScript(NefFile contract, Dictionary<string, JToken> config)
        {
            var operation = config.TryGetValue("operation", out var op) 
                ? op.Value<string>() : throw new InvalidDataException("missing operation config");

            using var builder = new ScriptBuilder();
            builder.EmitAppCall(contract.ScriptHash, operation, ParseArguments(config).ToArray());
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

        static IEnumerable<(StorageKey key, StorageItem item)> ParseStorage(int contractId, Dictionary<string, JToken> config)
        {
            if (config.TryGetValue("storage", out var token)
                && token != null)
            {
                return ParseStorage(contractId, token);
            }

            return Enumerable.Empty<(StorageKey key, StorageItem item)>();
        }

        static IEnumerable<(StorageKey key, StorageItem item)> ParseStorage(int contractId, JToken token)
        {
            return token.Select(t =>
            {
                var key = new StorageKey
                {
                    Id = contractId,
                    Key = ConvertString(t["key"]),
                };

                var item = new StorageItem
                {
                    Value = ConvertString(t["value"]),
                    IsConstant = t.Value<bool?>("constant") ?? false
                };
                return (key, item);
            });

            static byte[] ConvertString(JToken? token)
            {
                var arg = ParseArg(token?.Value<string>() ?? string.Empty);

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
        static ContractParameter ParseArg(string arg)
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

        static ContractParameter ParseArg(JToken arg)
        {
            return arg.Type switch
            {
                JTokenType.String => ParseArg(arg.Value<string>()),
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
                _ => throw new Exception()
            };
        }

        static IEnumerable<ContractParameter> ParseArgs(JToken? args)
            => args == null
                ? Enumerable.Empty<ContractParameter>()
                : args.Select(ParseArg);
    }
}
