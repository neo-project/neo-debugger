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
    partial class InteropService : IInteropService
    {
        private readonly IBlockchainStorage? blockchain;
        private readonly TriggerType trigger = TriggerType.Application;
        private readonly EmulatedStorage storage;
        private readonly WitnessChecker witnessChecker = null!;
        private readonly Action<OutputEvent> sendOutput;
        private readonly IReadOnlyDictionary<(UInt160, string), DebugInfo.Event> events;
        private readonly Dictionary<uint, Func<ExecutionEngine, bool>> methods = new Dictionary<uint, Func<ExecutionEngine, bool>>();
        private readonly Dictionary<uint, string> methodNames = new Dictionary<uint, string>();

        public InteropService(IBlockchainStorage? blockchain, EmulatedStorage storage, TriggerType trigger, WitnessChecker witnessChecker, Action<OutputEvent> sendOutput,
            IEnumerable<(UInt160 scriptHash, DebugInfo.Event info)> events)
        {
            this.sendOutput = sendOutput;
            this.blockchain = blockchain;
            this.storage = storage;
            this.witnessChecker = witnessChecker;
            this.trigger = trigger;
            this.events = events.ToDictionary(t => (t.scriptHash, t.info.Name), t => t.info);

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

        public IVariableContainer GetStorageContainer(IVariableContainerSession session, in UInt160 scriptHash)
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

        public EvaluateResponse EvaluateStorageExpression(IVariableContainerSession session, in UInt160 scriptHash, EvaluateArguments args)
        {
            bool TryFindStorage(int keyHash, in UInt160 _scriptHash, out (ReadOnlyMemory<byte> key, StorageItem item) value)
            {
                foreach (var (key, item) in storage.EnumerateStorage(_scriptHash))
                {
                    if (key.Span.GetSequenceHashCode() == keyHash)
                    {
                        value = (key, item);
                        return true;
                    }
                }

                value = default;
                return false;
            }

            var (typeHint, index, name) = DebugSession.ParseEvalExpression(args.Expression);
            var match = storageRegex.Match(name);

            if (!index.HasValue && match.Success
                && int.TryParse(match.Groups[1].Value, NumberStyles.HexNumber, null, out var keyHash)
                && TryFindStorage(keyHash, scriptHash, out var _storage))
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
            => methodNames.GetValueOrDefault(methodHash, string.Empty)!;

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