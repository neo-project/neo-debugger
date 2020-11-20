using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
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
    static class LaunchConfigurationParser
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
                yield return ParseArgument(
                    method.Parameters[i],
                    i < args.Count ? args[i] : null);
            }

            JArray GetArgsConfig()
            {
                if (config.TryGetValue("invocation", out var invocation))
                {
                    if (invocation["traceFile"] != null) throw new InvalidOperationException("traceFile invocation only supported on Neo 3 contracts");
                    if (invocation["oracleResponse"] != null) throw new InvalidOperationException("oracleResponse invocation only supported on Neo 3 contracts");
                    // TODO: invoke file support
                    if (invocation["invokeFile"] != null) throw new InvalidOperationException("invokeFile invocation only supported on Neo 3 contracts");

                    var operation = invocation["operation"];
                    var args = invocation["args"];
                    if (operation == null)
                    {
                        return args == null ? new JArray() : args.Type == JTokenType.Array ? (JArray)args : new JArray(args);
                    }

                    var items = args == null ? Enumerable.Empty<JToken>()
                        : args.Type == JTokenType.Array ? args : (IEnumerable<JToken>)new[] { args };
                    return new JArray(items.Prepend(operation));
                }

                return new JArray();
            }
        }

        static ContractArgument ParseArgument((string name, string type) param, JToken? arg)
        {
            if (param.type.Length == 0)
                return ParseArgument(arg);

            var type = param.type switch
            {
                "Integer" => ContractParameterType.Integer,
                "String" => ContractParameterType.String,
                "Array" => ContractParameterType.Array,
                "Boolean" => ContractParameterType.Boolean,
                "ByteArray" => ContractParameterType.ByteArray,
                _ => throw new NotImplementedException(),
            };

            return new ContractArgument(type, ParseArgumentValue(type, arg));
        }

        static object ParseArgumentValue(ContractParameterType paramType, JToken? arg)
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
                    return arg.Select(ParseArgument).ToArray();
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

        static ContractArgument ParseArgument(JToken? arg)
        {
            if (arg == null)
            {
                return new ContractArgument(ContractParameterType.ByteArray, BigInteger.Zero);
            }

            return arg.Type switch
            {
                JTokenType.Boolean => new ContractArgument(ContractParameterType.Boolean, arg.Value<bool>()),
                JTokenType.Integer => new ContractArgument(ContractParameterType.Integer, new BigInteger(arg.Value<int>())),
                JTokenType.Array => new ContractArgument(ContractParameterType.Array, arg.Select(ParseArgument).ToArray()),
                JTokenType.String => ParseArgumentString(arg.Value<string>()),
                JTokenType.Object => ParseArgumentObject((JObject)arg),
                _ => throw new ArgumentException(nameof(arg))
            };
        }

        static ContractArgument ParseArgumentString(string value)
        {
            if (value.TryParseBigInteger(out var bigInteger))
            {
                return new ContractArgument(ContractParameterType.ByteArray, bigInteger);
            }

            return new ContractArgument(ContractParameterType.String, value);
        }

        static ContractArgument ParseArgumentObject(JObject arg)
        {
            var type = Enum.Parse<ContractParameterType>(arg.Value<string>("type"));
            object value = type switch
            {
                ContractParameterType.ByteArray => BigInteger.Parse(arg.Value<string>("value")).ToByteArray(),
                ContractParameterType.Signature => BigInteger.Parse(arg.Value<string>("value")).ToByteArray(),
                ContractParameterType.Boolean => arg.Value<bool>("value"),
                ContractParameterType.Integer => BigInteger.Parse(arg.Value<string>("value")),
                ContractParameterType.Hash160 => UInt160ToArray(arg.Value<string>("value")),
                ContractParameterType.Hash256 => UInt256ToArray(arg.Value<string>("value")),
                // ContractParameterType.PublicKey => ECPoint.Parse(json.Value<string>("value"), ECCurve.Secp256r1),
                ContractParameterType.String => arg.Value<string>("value"),
                ContractParameterType.Array => arg["value"].Select(ParseArgument).ToArray(),
                ContractParameterType.Map => arg["value"].Select(ParseMapElement).ToArray(),
                _ => throw new ArgumentException(nameof(arg) + $" {type}"),
            };

            return new ContractArgument(type, value);

            static byte[] UInt160ToArray(string value)
            {
                if (UInt160.TryParse(value, out var result)
                    && result.TryToArray(out var array))
                {
                    return array;
                }

                throw new ArgumentException(nameof(value));
            }

            static byte[] UInt256ToArray(string value)
            {
                if (UInt256.TryParse(value, out var result)
                    && result.TryToArray(out var array))
                {
                    return array;
                }

                throw new ArgumentException(nameof(value));
            }

            static KeyValuePair<ContractArgument, ContractArgument> ParseMapElement(JToken json)
            {
                var key = ParseArgument(json["key"] ?? throw new ArgumentException(nameof(json)));
                var value = ParseArgument(json["value"] ?? throw new ArgumentException(nameof(json)));
                return KeyValuePair.Create(key, value);
            }
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
                        t.Value<ushort>("n")));

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

            return token.Select(t =>
            {
                var key = ConvertString(t["key"]);
                var value = ConvertString(t["value"]);
                bool constant = t.Value<bool?>("constant") ?? false;
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
