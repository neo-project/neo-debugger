using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
        public static DebugSession CreateDebugSession(LaunchArguments arguments, Action<DebugEvent> sendEvent, DebugSession.DebugView defaultDebugView)
        {
            var contract = Contract.Load(arguments.ConfigurationProperties["program"].Value<string>());


            // session = DebugSession.Create(contract, arguments, Protocol.SendEvent, defaultDebugView);


            throw new NotImplementedException();
        }

        public static IBlockchainStorage? ParseBlockchain(Dictionary<string, JToken> config)
        {
            if (config.TryGetValue("checkpoint", out var checkpoint))
            {
                return NeoFx.RocksDb.RocksDbStore.OpenCheckpoint(checkpoint.Value<string>());
            }

            return null;
        }

        public static (IEnumerable<CoinReference> inputs, IEnumerable<TransactionOutput> outputs)
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

        public static IEnumerable<(byte[] key, byte[] value, bool constant)>
            ParseStorage(Dictionary<string, JToken> config)
        {
            if (config.TryGetValue("storage", out var token))
            {
                return token.Select(t =>
                    (ConvertString(t["key"]),
                    ConvertString(t["value"]),
                    t.Value<bool?>("constant") ?? false));
            }

            return Enumerable.Empty<(byte[], byte[], bool)>();

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

        public static (TriggerType, WitnessChecker) ParseRuntime(Dictionary<string, JToken> config)
        {
            if (config.TryGetValue("runtime", out var token))
            {
                // TODO: check to make sure this doesn't throw if trigger json value is missing
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

// public static DebugExecutionEngine Create(Contract contract, LaunchArguments arguments, Action<OutputEvent> sendOutput)
// {
//     throw new Exception();
//     // var blockchain = GetBlockchain(arguments.ConfigurationProperties);
//     // var (inputs, outputs) = GetUtxo(arguments.ConfigurationProperties, blockchain);

//     // var tx = new InvocationTransaction(contract.Script, Fixed8.Zero, 1, default,
//     //     inputs.ToArray(), outputs.ToArray(), default);
//     // var container = new ModelAdapters.TransactionAdapter(tx);

//     // var table = new ScriptTable();
//     // table.Add(contract.Script);

//     // //TODO: load these from launch config
//     // var emulatedStorage = new EmulatedStorage(null, Enumerable.Empty<(StorageKey, StorageItem)>());
//     // var witnessChecker = new WitnessChecker(true);
//     // var events = Enumerable.Empty<(UInt160, string, EventDebugInfo)>();

//     // var interopService = new InteropService(null, emulatedStorage, TriggerType.Application, witnessChecker, sendOutput, events);
//     // return new DebugExecutionEngine(container, table, interopService);
// }

//         static public DebugSession Create(Contract contract, LaunchArguments arguments, Action<DebugEvent> sendEvent, DebugView defaultDebugView)
// {
//     throw new Exception();

// var contractArgs = GetArguments(contract.EntryPoint).ToArray();
// var returnTypes = GetReturnTypes().ToArray();

// var engine = DebugExecutionEngine.Create(contract, arguments, outputEvent => sendEvent(outputEvent));
// return new DebugSession(engine, contract, sendEvent, contractArgs, returnTypes, defaultDebugView);

// JArray GetArgsConfig()
// {
//     if (arguments.ConfigurationProperties.TryGetValue("args", out var args))
//     {
//         if (args is JArray jArray)
//         {
//             return jArray;
//         }

//         return new JArray(args);
//     }

//     return new JArray();
// }

// IEnumerable<ContractArgument> GetArguments(MethodDebugInfo method)
// {
//     var args = GetArgsConfig();
//     for (int i = 0; i < method.Parameters.Count; i++)
//     {
//         yield return ConvertArgument(
//             method.Parameters[i],
//             i < args.Count ? args[i] : null);
//     }
// }

// IEnumerable<string> GetReturnTypes()
// {
//     if (arguments.ConfigurationProperties.TryGetValue("return-types", out var returnTypes))
//     {
//         foreach (var returnType in returnTypes)
//         {
//             yield return Helpers.CastOperations[returnType.Value<string>()];
//         }
//     }
// }
// }


// private static ContractArgument ConvertArgument(JToken arg)
// {
//     switch (arg.Type)
//     {
//         case JTokenType.Integer:
//             return new ContractArgument(ContractParameterType.Integer, new BigInteger(arg.Value<int>()));
//         case JTokenType.String:
//             var value = arg.Value<string>();
//             if (value.TryParseBigInteger(out var bigInteger))
//             {
//                 return new ContractArgument(ContractParameterType.Integer, bigInteger);
//             }
//             else
//             {
//                 return new ContractArgument(ContractParameterType.String, value);
//             }
//         default:
//             throw new NotImplementedException($"DebugAdapter.ConvertArgument {arg.Type}");
//     }
// }

// private static object ConvertArgumentToObject(ContractParameterType paramType, JToken? arg)
// {
//     if (arg == null)
//     {
//         return paramType switch
//         {
//             ContractParameterType.Boolean => false,
//             ContractParameterType.String => string.Empty,
//             ContractParameterType.Array => Array.Empty<ContractArgument>(),
//             _ => BigInteger.Zero,
//         };
//     }

//     switch (paramType)
//     {
//         case ContractParameterType.Boolean:
//             return arg.Value<bool>();
//         case ContractParameterType.Integer:
//             return arg.Type == JTokenType.Integer
//                 ? new BigInteger(arg.Value<int>())
//                 : BigInteger.Parse(arg.ToString());
//         case ContractParameterType.String:
//             return arg.ToString();
//         case ContractParameterType.Array:
//             return arg.Select(ConvertArgument).ToArray();
//         case ContractParameterType.ByteArray:
//             {
//                 var value = arg.ToString();
//                 if (value.TryParseBigInteger(out var bigInteger))
//                 {
//                     return bigInteger;
//                 }

//                 var byteCount = Encoding.UTF8.GetByteCount(value);
//                 using var owner = MemoryPool<byte>.Shared.Rent(byteCount);
//                 var span = owner.Memory.Span.Slice(0, byteCount);
//                 Encoding.UTF8.GetBytes(value, span);
//                 return new BigInteger(span);
//             }
//     }
//     throw new NotImplementedException($"DebugAdapter.ConvertArgument {paramType} {arg}");
// }

// private static ContractArgument ConvertArgument((string name, string type) param, JToken? arg)
// {
//     var type = param.type switch
//     {
//         "Integer" => ContractParameterType.Integer,
//         "String" => ContractParameterType.String,
//         "Array" => ContractParameterType.Array,
//         "Boolean" => ContractParameterType.Boolean,
//         "ByteArray" => ContractParameterType.ByteArray,
//         "" => ContractParameterType.ByteArray,
//         _ => throw new NotImplementedException(),
//     };

//     return new ContractArgument(type, ConvertArgumentToObject(type, arg));
// }
