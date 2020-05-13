using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using NeoDebug.Models;
using NeoFx;
using NeoFx.Models;
using NeoFx.Storage;
using Newtonsoft.Json.Linq;

namespace NeoDebug
{
    class LaunchConfigurationParser
    {
        public static async Task<DebugSession> CreateDebugSession(LaunchArguments arguments, Action<DebugEvent> sendEvent, DebugSession.DebugView defaultDebugView)
        {
            var config = arguments.ConfigurationProperties;
            var sourceFileMap = ParseSourceFileMap(config);
            var contract = await Contract.Load(config["program"].Value<string>(), sourceFileMap).ConfigureAwait(false);
            var storages = ParseStorage(contract.ScriptHash, config);
            var (storedContracts, storedContractStorages) = await ParseStoredContracts(config, sourceFileMap);

            var invokeScript = BuildInvokeScript(contract.ScriptHash, ParseArguments(contract.EntryPoint, config));
            var engine = CreateExecutionEngine(invokeScript, 
                storedContracts.Append(contract),
                storedContractStorages.Concat(storages),
                config, 
                sendEvent);

            var returnTypes = ParseReturnTypes(config).ToList();
            return new DebugSession(engine, storedContracts.Append(contract), sendEvent, returnTypes, defaultDebugView);

            static byte[] BuildInvokeScript(UInt160 scriptHash, IEnumerable<ContractArgument> arguments)
            {
                using var builder = new Neo.VM.ScriptBuilder();
                foreach (var argument in arguments.Reverse())
                {
                    argument.EmitPush(builder);
                }

                builder.EmitAppCall(scriptHash);
                return builder.ToArray();
            }
        }

        static DebugExecutionEngine CreateExecutionEngine(byte[] invokeScript,
                                                          IEnumerable<Contract> contracts,
                                                          IEnumerable<(StorageKey, StorageItem)> storages,
                                                          Dictionary<string, JToken> config,
                                                          Action<OutputEvent> sendOutput)
        {
            var blockchain = CreateBlockchainStorage(config);
            var (inputs, outputs) = ParseUtxo(blockchain, config);
            var tx = new InvocationTransaction(invokeScript, Fixed8.Zero, 1, default,
                inputs.ToArray(), outputs.ToArray(), default);
            var container = new ModelAdapters.TransactionAdapter(tx);

            var table = new ScriptTable();
            foreach (var contract in contracts)
            {
                table.Add(contract.Script);
            }

            var events = contracts.SelectMany(c => c.DebugInfo.Events.Select(e => (c.ScriptHash, e)));
            var emulatedStorage = new EmulatedStorage(blockchain, storages);
            var (trigger, witnessChecker) = ParseRuntime(config);

            var interopService = new InteropService(blockchain, emulatedStorage, trigger, witnessChecker, sendOutput, events);

            var engine = new DebugExecutionEngine(container, table, interopService);
            engine.LoadScript(invokeScript);
            return engine;
        }

        static IBlockchainStorage? CreateBlockchainStorage(Dictionary<string, JToken> config)
        {
            if (config.TryGetValue("checkpoint", out var checkpoint))
            {
                return NeoFx.RocksDb.RocksDbStore.OpenCheckpoint(checkpoint.Value<string>());
            }

            return null;
        }

        static IEnumerable<string> ParseReturnTypes(Dictionary<string, JToken> config)
        {
            if (config.TryGetValue("return-types", out var returnTypes))
            {
                foreach (var returnType in returnTypes)
                {
                    yield return DebugSession.CastOperations[returnType.Value<string>()];
                }
            }
        }

        static IEnumerable<ContractArgument> ParseArguments(DebugInfo.Method method, Dictionary<string, JToken> config)
        {
            var args = GetArgsConfig();
            for (int i = 0; i < method.Parameters.Count; i++)
            {
                yield return ConvertArgument(
                    method.Parameters[i],
                    i < args.Count ? args[i] : null);
            }

            JArray GetArgsConfig()
            {
                if (config.TryGetValue("args", out var args))
                {
                    if (args is JArray jArray)
                    {
                        return jArray;
                    }

                    return new JArray(args);
                }

                return new JArray();
            }
        }

        static ContractArgument ConvertArgument(JToken arg)
        {
            switch (arg.Type)
            {
                case JTokenType.Integer:
                    return new ContractArgument(ContractParameterType.Integer, new BigInteger(arg.Value<int>()));
                case JTokenType.String:
                    var value = arg.Value<string>();
                    if (value.TryParseBigInteger(out var bigInteger))
                    {
                        return new ContractArgument(ContractParameterType.Integer, bigInteger);
                    }
                    else
                    {
                        return new ContractArgument(ContractParameterType.String, value);
                    }
                default:
                    throw new NotImplementedException($"DebugAdapter.ConvertArgument {arg.Type}");
            }
        }

        static object ConvertArgumentToObject(ContractParameterType paramType, JToken? arg)
        {
            if (arg == null)
            {
                return paramType switch
                {
                    ContractParameterType.Boolean => false,
                    ContractParameterType.String => string.Empty,
                    ContractParameterType.Array => Array.Empty<ContractArgument>(),
                    _ => BigInteger.Zero,
                };
            }

            switch (paramType)
            {
                case ContractParameterType.Boolean:
                    return arg.Value<bool>();
                case ContractParameterType.Integer:
                    return arg.Type == JTokenType.Integer
                        ? new BigInteger(arg.Value<int>())
                        : BigInteger.Parse(arg.ToString());
                case ContractParameterType.String:
                    return arg.ToString();
                case ContractParameterType.Array:
                    return arg.Select(ConvertArgument).ToArray();
                case ContractParameterType.ByteArray:
                    {
                        var value = arg.ToString();
                        if (value.TryParseBigInteger(out var bigInteger))
                        {
                            return bigInteger;
                        }

                        var byteCount = Encoding.UTF8.GetByteCount(value);
                        using var owner = MemoryPool<byte>.Shared.Rent(byteCount);
                        var span = owner.Memory.Span.Slice(0, byteCount);
                        Encoding.UTF8.GetBytes(value, span);
                        return new BigInteger(span);
                    }
            }
            throw new NotImplementedException($"DebugAdapter.ConvertArgument {paramType} {arg}");
        }

        static ContractArgument ConvertArgument((string name, string type) param, JToken? arg)
        {
            var type = param.type switch
            {
                "Integer" => ContractParameterType.Integer,
                "String" => ContractParameterType.String,
                "Array" => ContractParameterType.Array,
                "Boolean" => ContractParameterType.Boolean,
                "ByteArray" => ContractParameterType.ByteArray,
                "" => ContractParameterType.ByteArray,
                _ => throw new NotImplementedException(),
            };

            return new ContractArgument(type, ConvertArgumentToObject(type, arg));
        }

        static (IEnumerable<CoinReference> inputs, IEnumerable<TransactionOutput> outputs)
            ParseUtxo(IBlockchainStorage? blockchain, Dictionary<string, JToken> config)
        {
            static UInt160 ParseAddress(string address) =>
                UInt160.TryParse(address, out var result) ? result : address.ToScriptHash();

            static UInt256 GetAssetId(IBlockchainStorage blockchain, string asset)
            {
                if (string.Compare("neo", asset, true) == 0)
                    return blockchain.GoverningTokenHash;

                if (string.Compare("gas", asset, true) == 0)
                    return blockchain.UtilityTokenHash;

                return UInt256.Parse(asset);
            }

            if (blockchain != null && config.TryGetValue("utxo", out var utxo))
            {
                var _inputs = (utxo["inputs"] ?? Enumerable.Empty<JToken>())
                    .Select(t => new CoinReference(
                        UInt256.Parse(t.Value<string>("txid")),
                        t.Value<ushort>("value")));

                var _outputs = (utxo["outputs"] ?? Enumerable.Empty<JToken>())
                    .Select(t => new TransactionOutput(
                        GetAssetId(blockchain, t.Value<string>("asset")),
                        Fixed8.Create(t.Value<long>("value")),
                        ParseAddress(t.Value<string>("address"))));

                return (_inputs, _outputs);
            }

            return (Enumerable.Empty<CoinReference>(), Enumerable.Empty<TransactionOutput>());
        }

        static IEnumerable<(StorageKey key, StorageItem item)> ParseStorage(UInt160 scriptHash, Dictionary<string, JToken> config)
        {
            if (config.TryGetValue("storage", out var token))
            {
                return ParseStorage(scriptHash, token);
            }

            return Enumerable.Empty<(StorageKey key, StorageItem item)>();
        }

        static IEnumerable<(StorageKey key, StorageItem item)> ParseStorage(UInt160 scriptHash, JToken? token)
        {
            if (token == null)
            {
                return Enumerable.Empty<(StorageKey, StorageItem)>();
            }

            return token.Select(t => {
                var key = ConvertString(t["key"]);
                var value = ConvertString(t["value"]);
                bool constant = t.Value<bool?>() ?? false;
                return (new StorageKey(scriptHash, key), new StorageItem(value, constant));
            });

            static byte[] ConvertString(JToken? token)
            {
                var value = token?.Value<string>() ?? string.Empty;
                if (value.TryParseBigInteger(out var bigInteger))
                {
                    return bigInteger.ToByteArray();
                }
                return Encoding.UTF8.GetBytes(value);
            }
        }

        static async Task<(List<Contract> contracts, IEnumerable<(StorageKey key, StorageItem item)> storages)>
            ParseStoredContracts(Dictionary<string, JToken> config, IReadOnlyDictionary<string, string> sourceFileMap)
        {
            var contracts = new List<Contract>();
            var storages = Enumerable.Empty<(StorageKey, StorageItem)>();

            if (config.TryGetValue("stored-contracts", out var storedContracts))
            {
                foreach (var storedContract in storedContracts)
                {
                    if (storedContract.Type == JTokenType.String)
                    {
                        var contract = await Contract.Load(storedContract.Value<string>(), sourceFileMap).ConfigureAwait(false);
                        contracts.Add(contract);
                    }
                    else if (storedContract.Type == JTokenType.Object)
                    {
                        var contract = await Contract.Load(storedContract.Value<string>("program"), sourceFileMap).ConfigureAwait(false);
                        contracts.Add(contract);

                        var storage = ParseStorage(contract.ScriptHash, storedContract["storage"]);
                        storages = storages.Concat(storage);
                    }
                    else 
                    {
                        throw new Exception("invalid stored-contract value");
                    }
                }
            }

            return (contracts, storages); 
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

            static byte[] ParseWitness(JToken value)
            {
                if (value.Value<string>().TryParseBigInteger(out var bigInt))
                {
                    return bigInt.ToByteArray();
                }

                throw new Exception($"TryParseBigInteger for {value} failed");
            }
        }
    }
}
