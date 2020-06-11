using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo.Ledger;
using Neo.Persistence;
using Neo.SmartContract;
using Neo.SmartContract.Manifest;
using Neo.VM;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NeoDebug.Neo3
{
    static class LaunchConfigParser
    {
        public static DebugSession CreateDebugSession(LaunchArguments launchArguments, Action<DebugEvent> sendEvent, DebugView defaultDebugView)
        {
            var config = launchArguments.ConfigurationProperties;
            var program = config["program"].Value<string>();
            var (contract, manifest) = LoadContract(program);
            var debugInfo = DebugInfoParser.Load(program);

            IStore memoryStore = new MemoryStore();
            var id = AddContract(memoryStore, contract, manifest);
            AddStorage(memoryStore, ParseStorage(id, config));

            var engine = new DebugApplicationEngine(new SnapshotView(memoryStore));
            var invokeScript = CreateLaunchScript(contract, config);
            engine.LoadScript(invokeScript);

            return new DebugSession(engine, sendEvent, new DebugInfo[]{ debugInfo }, DebugView.Source);

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

                var contractState = new Neo.Ledger.ContractState
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
                var value = token?.Value<string>() ?? string.Empty;
                // if (value.TryParseBigInteger(out var bigInteger))
                // {
                //     return bigInteger.ToByteArray();
                // }
                return Encoding.UTF8.GetBytes(value);
            }
        }

        static IEnumerable<ContractParameter> ParseArguments(Dictionary<string, JToken> config)
        {
            if (config.TryGetValue("args", out var args))
            {
                if (args is JArray jArgs)
                {
                    for (int i = 0; i < jArgs.Count; i++)
                    {
                        yield return ParseArgument(jArgs[i]);
                    }
                }
                else
                {
                    yield return ParseArgument(args);
                }
            }
        }

        static ContractParameter ParseArgument(JToken arg)
        {
            return arg.Type switch
            {
                JTokenType.Boolean => new ContractParameter(ContractParameterType.Boolean) { Value = arg.Value<bool>() },
                JTokenType.Integer => new ContractParameter(ContractParameterType.Integer) { Value = new System.Numerics.BigInteger(arg.Value<int>()) },
                JTokenType.Array => new ContractParameter(ContractParameterType.Array) { Value = arg.Select(ParseArgument).ToList() },
                JTokenType.String => new ContractParameter(ContractParameterType.String) { Value = arg.Value<string>() },
                _ => throw new ArgumentException(nameof(arg))
            };
        }
    }
}
