using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo.VM;
using NeoDebug.Models;
using NeoDebug.VariableContainers;
using NeoFx;
using NeoFx.Models;
using NeoFx.Storage;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;

namespace NeoDebug
{
    internal partial class InteropService : IInteropService
    {
        public enum TriggerType
        {
            Verification = 0x00,
            Application = 0x10,
        }

        private readonly Dictionary<uint, Func<ExecutionEngine, bool>> methods = new Dictionary<uint, Func<ExecutionEngine, bool>>();
        private readonly Dictionary<uint, string> methodNames = new Dictionary<uint, string>();
        private readonly EmulatedStorage storage;

        private readonly IBlockchainStorage? blockchain;
        private readonly Action<OutputEvent> sendOutput;

        private static IEnumerable<(byte[] key, byte[] value, bool constant)>
            GetStorage(Dictionary<string, JToken> config)
        {
            static byte[] ConvertString(JToken? token)
            {
                var value = token?.Value<string>() ?? string.Empty;
                if (value.TryParseBigInteger(out var bigInteger))
                {
                    return bigInteger.ToByteArray();
                }
                return Encoding.UTF8.GetBytes(value);
            }

            if (config.TryGetValue("storage", out var token))
            {
                return token.Select(t =>
                    (ConvertString(t["key"]),
                    ConvertString(t["value"]),
                    t.Value<bool?>("constant") ?? false));
            }

            return Enumerable.Empty<(byte[], byte[], bool)>();
        }

        public InteropService(Contract contract, IBlockchainStorage? blockchain, Dictionary<string, JToken> config, Action<OutputEvent> sendOutput)
        {
            throw new NotImplementedException();
        }

        public InteropService(IBlockchainStorage? blockchain, EmulatedStorage storage, TriggerType trigger, WitnessChecker witnessChecker, Action<OutputEvent> sendOutput)
        {
            // static byte[] ParseWitness(JToken value)
            // {
            //     if (value.Value<string>().TryParseBigInteger(out var bigInt))
            //     {
            //         return bigInt.ToByteArray();
            //     }

            //     throw new Exception($"TryParseBigInteger for {value} failed");
            // }

            this.sendOutput = sendOutput;
            this.blockchain = blockchain;
            this.storage = storage;
            this.witnessChecker = witnessChecker;
            this.trigger = trigger;
            // storage = new EmulatedStorage(blockchain);

            // foreach (var item in GetStorage(config))
            // {
            //     var storageKey = new StorageKey(contract.ScriptHash, item.key);
            //     storage.TryPut(storageKey, item.value, item.constant);
            // }

            // if (config.TryGetValue("runtime", out var token))
            // {
            //     trigger = "verification".Equals(token.Value<string>("trigger"), StringComparison.InvariantCultureIgnoreCase)
            //         ? TriggerType.Verification : TriggerType.Application;

            //     var witnessesJson = token["witnesses"];
            //     if (witnessesJson?.Type == JTokenType.Object)
            //     {
            //         checkWitnessBypass = true;
            //         checkWitnessBypassValue = witnessesJson.Value<bool>("check-result");
            //     }
            //     else if (witnessesJson?.Type == JTokenType.Array)
            //     {
            //         checkWitnessBypass = false;
            //         witnesses = witnessesJson.Select(ParseWitness);
            //     }
            // }

            RegisterAccount(Register);
            RegisterAsset(Register);
            RegisterBlock(Register);
            RegisterBlockchain(Register);
            RegisterContract(Register);
            RegisterEnumerator(Register);
            RegisterExecutionEngine(Register);
            RegisterHeader(Register);
            RegisterRuntime(Register);
            RegisterStorage(Register);
            RegisterTransaction(Register);
        }

        internal IVariableContainer GetStorageContainer(IVariableContainerSession session, UInt160 scriptHash)
        {
            return new EmulatedStorageContainer(session, scriptHash, storage);
        }

        static readonly Regex storageRegex = new Regex(@"^\$storage\[([0-9a-fA-F]{8})\]\.(key|value)$");

        private EvaluateResponse EvaluateStorageExpression(IVariableContainerSession session, ReadOnlyMemory<byte> memory, string? typeHint)
        {
            switch (typeHint)
            {
                case "Boolean":
                    return new EvaluateResponse()
                    {
                        Result = (new BigInteger(memory.Span) != BigInteger.Zero).ToString(),
                        Type = "#Boolean",
                    };
                case "Integer":
                    return new EvaluateResponse()
                    {
                        Result = new BigInteger(memory.Span).ToString(),
                        Type = "#Integer",
                    };
                case "String":
                    return new EvaluateResponse()
                    {
                        Result = Encoding.UTF8.GetString(memory.Span),
                        Type = "#String",
                    };
                default:
                case "HexString":
                    return new EvaluateResponse()
                    {
                        Result = new BigInteger(memory.Span).ToHexString(),
                        Type = "#ByteArray"
                    };
                case "ByteArray":
                    {
                        var variable = ByteArrayContainer.Create(session, memory, string.Empty, true);
                        return new EvaluateResponse()
                        {
                            Result = variable.Value,
                            VariablesReference = variable.VariablesReference,
                            Type = variable.Type,
                        };
                    }
            }
        }

        public EvaluateResponse EvaluateStorageExpression(IVariableContainerSession session, EvaluateArguments args)
        {
            bool TryFindStorage(int keyHash, out (ReadOnlyMemory<byte> key, StorageItem item) value)
            {
                // foreach (var (key, item) in storage.EnumerateStorage(contract.ScriptHash))
                // {
                //     if (key.Span.GetSequenceHashCode() == keyHash)
                //     {
                //         value = (key, item);
                //         return true;
                //     }
                // }

                value = default;
                return false;
            }

            var (typeHint, index, name) = Helpers.ParseEvalExpression(args.Expression);
            var match = storageRegex.Match(name);

            if (!index.HasValue && match.Success
                && int.TryParse(match.Groups[1].Value, NumberStyles.HexNumber, null, out var keyHash)
                && TryFindStorage(keyHash, out var _storage))
            {
                switch (match.Groups[2].Value)
                {
                    case "key":
                        return EvaluateStorageExpression(session, _storage.key, typeHint);
                    case "value":
                        return EvaluateStorageExpression(session, _storage.item.Value, typeHint);
                }
            }

            return DebugAdapter.FailedEvaluation;
        }

        protected void Register(string methodName, Func<ExecutionEngine, bool> handler, int price)
        {
            if (!HashHelpers.TryInteropMethodHash(methodName, out var value))
            {
                throw new ArgumentException(nameof(methodName));
            }

            methods.Add(value, handler);
            methodNames.Add(value, methodName);
        }

        public string GetMethodName(uint methodHash)
        {
            if (methodNames.TryGetValue(methodHash, out var methodName))
            {
                return methodName;
            }

            return string.Empty;
        }

        bool IInteropService.Invoke(byte[] method, ExecutionEngine engine)
        {
            static bool TryInteropMethodHash(Span<byte> method, out uint value)
            {
                if (method.Length == 4)
                {
                    value = BitConverter.ToUInt32(method);
                    return true;
                }

                return HashHelpers.TryInteropMethodHash(method, out value);
            }

            if (TryInteropMethodHash(method, out var hash)
                && methods.TryGetValue(hash, out var func))
            {
                try
                {
                    return func(engine);
                }
                catch (Exception ex)
                {
                    var methodHex = new BigInteger(method).ToHexString();
                    sendOutput(new OutputEvent()
                    {
                        Category = OutputEvent.CategoryValue.Stderr,
                        Output = $"Exception in {methodHex} method: {ex}\n",
                    });
                }
            }
            else
            {
                var methodHex = new BigInteger(method).ToHexString();
                sendOutput(new OutputEvent()
                {
                    Category = OutputEvent.CategoryValue.Stderr,
                    Output = $"Invalid interop method {methodHex}\n",
                });
            }

            return false;
        }
    }
}
