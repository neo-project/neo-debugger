using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo;
using Neo.BlockchainToolkit.Models;

using StackItem = Neo.VM.Types.StackItem;
using StackItemType = Neo.VM.Types.StackItemType;

namespace NeoDebug.Neo3
{
    class ExpressionEvaluator
    {
        public const string ARG_SLOTS_PREFIX = "#arg";
        public const string EVAL_STACK_PREFIX = "#eval";
        public const string LOCAL_SLOTS_PREFIX = "#local";
        public const string RESULT_STACK_PREFIX = "#result";
        public const string STATIC_SLOTS_PREFIX = "#static";
        public const string STORAGE_PREFIX = "#storage";

        public static readonly IReadOnlyDictionary<string, CastOperation> CastOperations = new Dictionary<string, CastOperation>()
        {
            { "int", CastOperation.Integer },
            { "integer", CastOperation.Integer },
            { "bool", CastOperation.Boolean },
            { "boolean", CastOperation.Boolean },
            { "string", CastOperation.String },
            { "str", CastOperation.String },
            { "hex", CastOperation.HexString },
            { "byte[]", CastOperation.ByteArray },
            { "addr", CastOperation.Address },
        };

        readonly IExecutionContext context;
        readonly IReadOnlyList<StackItem> resultStack;
        readonly StorageContainerBase storageContainer;
        readonly DebugInfo? debugInfo;
        readonly byte addressVersion;

        public ExpressionEvaluator(IApplicationEngine engine, IExecutionContext context, DebugInfo? debugInfo)
        {
            this.context = context;
            this.resultStack = engine.ResultStack;
            this.storageContainer = engine.GetStorageContainer(context.ScriptHash);
            this.debugInfo = debugInfo;
            this.addressVersion = engine.AddressVersion;
        }

        static bool IsValidRemaining(ReadOnlyMemory<char> expression) => expression.IsEmpty || expression.Span[0] == '.' || expression.Span[0] == '[';

        static bool TryEvaluateIndexedSlot(ReadOnlyMemory<char> expression, string prefix, IReadOnlyList<StackItem> slot, [MaybeNullWhen(false)] out StackItem result, out ReadOnlyMemory<char> remaining)
        {
            if (expression.StartsWith(prefix))
            {
                expression = expression.Slice(prefix.Length);

                var pos = 0;
                while (pos < expression.Length && char.IsDigit(expression.Span[pos]))
                {
                    pos++;
                }

                if (pos > 0)
                {
                    var slotIndexExpr = expression.Slice(0, pos);
                    expression = expression.Slice(pos);
                    if (IsValidRemaining(expression)
                        && int.TryParse(slotIndexExpr.Span, out var slotIndex)
                        && slotIndex < slot.Count)
                    {
                        result = slot[slotIndex];
                        remaining = expression.Slice(slotIndexExpr.Length);
                        return true;
                    }
                }
            }

            remaining = default;
            result = default;
            return false;
        }

        static bool TryEvaluateNamedSlot(ReadOnlyMemory<char> expression, IReadOnlyList<StackItem> slot, IReadOnlyList<DebugInfo.SlotVariable> variables,
            out (StackItem item, string type) result, out ReadOnlyMemory<char> remaining)
        {
            for (int i = 0; i < variables.Count; i++)
            {
                var variable = variables[i];
                if (expression.StartsWith(variable.Name) && variable.Index < slot.Count)
                {
                    remaining = expression.Slice(variable.Name.Length);
                    if (IsValidRemaining(remaining))
                    {
                        result = (slot[variable.Index], variable.Type);
                        return true;
                    }
                }
            }

            result = default;
            remaining = default;
            return false;
        }

        bool TryEvaluate(ReadOnlyMemory<char> expression, [MaybeNullWhen(false)] out StackItem result, out ReadOnlyMemory<char> remaining)
        {
            if (expression.StartsWith("#"))
            {
                if (storageContainer.TryEvaluate(expression, out result, out remaining)) return true;
                if (TryEvaluateIndexedSlot(expression, ARG_SLOTS_PREFIX, context.Arguments, out result, out remaining)) return true;
                if (TryEvaluateIndexedSlot(expression, EVAL_STACK_PREFIX, context.EvaluationStack, out result, out remaining)) return true;
                if (TryEvaluateIndexedSlot(expression, LOCAL_SLOTS_PREFIX, context.LocalVariables, out result, out remaining)) return true;
                if (TryEvaluateIndexedSlot(expression, RESULT_STACK_PREFIX, resultStack, out result, out remaining)) return true;
                if (TryEvaluateIndexedSlot(expression, STATIC_SLOTS_PREFIX, context.StaticFields, out result, out remaining)) return true;
            }
            else if (debugInfo is not null)
            {
                // TODO: flow type info out

                if (TryEvaluateNamedSlot(expression, context.StaticFields, debugInfo.StaticVariables, out var _result, out remaining))
                {
                    result = _result.item;
                    return true;
                }

                if (debugInfo.TryGetMethod(context.InstructionPointer, out var method))
                {
                    if (TryEvaluateNamedSlot(expression, context.Arguments, method.Parameters, out _result, out remaining))
                    {
                        result = _result.item;
                        return true;

                    }

                    if (TryEvaluateNamedSlot(expression, context.LocalVariables, method.Variables, out _result, out remaining))
                    {
                        result = _result.item;
                        return true;
                    }
                }
            }

            result = default;
            remaining = default;
            return false;
        }

        public bool TryEvaluate(IVariableManager manager, EvaluateArguments args, [MaybeNullWhen(false)] out EvaluateResponse response)
        {
            var (castOperation, expression) = ParseCastOperation(args.Expression.AsMemory());
            if (TryEvaluate(expression, out var result, out var remaining))
            {
                if (!remaining.IsEmpty) throw new Exception("Remaining not empty");

                return TryCreateResponse(manager, result, castOperation, out response);
            }

            response = null;
            return false;
        }

        bool TryCreateResponse(IVariableManager manager, StackItem item, CastOperation castOperation, [MaybeNullWhen(false)] out EvaluateResponse response)
        {
            try
            {
                switch (castOperation)
                {
                    case CastOperation.Address:
                        {
                            var hash = new UInt160(item.GetSpan());
                            var address = Neo.Wallets.Helper.ToAddress(hash, addressVersion);
                            response = new EvaluateResponse(address, 0);
                            return true;
                        }
                    case CastOperation.HexString:
                        {
                            if (item.IsNull)
                            {
                                response = new EvaluateResponse("<null>", 0);
                                return true;
                            }
                            else
                            {
                                var span = item.ConvertTo(StackItemType.ByteString).GetSpan();
                                response = new EvaluateResponse(span.ToHexString(), 0);
                                return true;
                            }
                        }
                    case CastOperation.Boolean:
                        {
                            response = new EvaluateResponse($"{item.GetBoolean()}", 0);
                            return true;
                        }
                    case CastOperation.Integer:
                        {
                            response = new EvaluateResponse($"{item.GetInteger()}", 0);
                            return true;
                        }
                    case CastOperation.String:
                        {
                            response = new EvaluateResponse(item.GetString(), 0);
                            return true;
                        }
                    case CastOperation.ByteArray:
                        {
                            if (item.IsNull)
                            {
                                response = new EvaluateResponse("<null>", 0);
                                return true;
                            }

                            if (ByteArrayContainer.TryCreate(item, out var container))
                            {
                                response = new EvaluateResponse($"byte[{container.Memory.Length}]", manager.Add(container));
                                return true;
                            }

                            break;
                        }
                }

                switch (item)
                {
                    case Neo.VM.Types.Struct @struct:
                    {
                        var container = new NeoArrayContainer(@struct);
                        response = new EvaluateResponse($"Struct[{@struct.Count}]", manager.Add(container));
                        return true;
                    }
                    case Neo.VM.Types.Array array:
                    {
                        var container = new NeoArrayContainer(array);
                        response = new EvaluateResponse($"Array[{array.Count}]", manager.Add(container));
                        return true;
                    }
                    case Neo.VM.Types.Boolean:
                    {
                        response = new EvaluateResponse($"{item.GetBoolean()}", 0);
                        return true;
                    }
                    case Neo.VM.Types.Buffer buffer:
                    {
                        var container = new ByteArrayContainer(buffer.InnerBuffer);
                        response = new EvaluateResponse($"Buffer[{buffer.InnerBuffer.Length}]", manager.Add(container));
                        return true;
                    }
                    case Neo.VM.Types.ByteString byteString:
                    {
                        var container = new ByteArrayContainer(byteString);
                        response = new EvaluateResponse($"ByteString[{item.GetSpan().Length}]", manager.Add(container));
                        return true;
                    }
                    case Neo.VM.Types.Integer:
                    {
                        response = new EvaluateResponse($"{item.GetInteger()}", 0);
                        return true;
                    }
                    case Neo.VM.Types.Map map:
                    {
                        var container = new NeoMapContainer(map);
                        response = new EvaluateResponse($"Map[{map.Count}]", manager.Add(container));
                        return true;
                    }
                    case Neo.VM.Types.Null:
                    {
                        response = new EvaluateResponse("<null>", 0);
                        return true;
                    }
                    case Neo.VM.Types.InteropInterface @interface:
                    {
                        var typeName = @interface.TryGetInteropType(out var type) ? type.Name : "unknown";
                        response = new EvaluateResponse($"InteropInterface<{typeName}>", 0);
                        return true;
                    }
                    case Neo.VM.Types.Pointer pointer: 
                    {
                        response = new EvaluateResponse($"Pointer[{pointer.Position}]", 0);
                        return true;
                    }
                }
            }
            catch { }

            response = default;
            return false;
        }

        static ReadOnlySpan<byte> GetSpan(StackItem item)
        {
            try
            {
                return item.GetSpan();
            }
            catch { return default; }
        }


        static (CastOperation castOperation, ReadOnlyMemory<char> remaining) ParseCastOperation(ReadOnlyMemory<char> expression)
        {

            if (expression.Length >= 1 && expression.Span[0] == '(')
            {
                expression = expression.Slice(1);
                foreach (var (key, operation) in CastOperations)
                {
                    if (expression.StartsWith(key) && expression.Span[key.Length] == ')')
                    {
                        return (operation, expression.Slice(key.Length + 1));
                    }
                }

                throw new Exception("invalid cast operation");
            }

            return (CastOperation.None, expression);
        }
    }
}
