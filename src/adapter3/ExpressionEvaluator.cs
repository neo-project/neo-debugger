using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo;
using Neo.BlockchainToolkit.Models;

using StackItem = Neo.VM.Types.StackItem;
using StackItemType = Neo.VM.Types.StackItemType;
using NeoArray = Neo.VM.Types.Array;
using ByteString = Neo.VM.Types.ByteString;
using Boolean = Neo.VM.Types.Boolean;

namespace NeoDebug.Neo3
{
    record ExpressionEvalContext(ReadOnlyMemory<char> Expression, StackItem Item, ContractType Type);

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

        public ExpressionEvaluator(IApplicationEngine engine, IExecutionContext context, DebugInfo? debugInfo, StorageView storageView)
        {
            this.context = context;
            this.resultStack = engine.ResultStack;
            this.storageContainer = engine.GetStorageContainer(context.ScriptHash, storageView);
            this.debugInfo = debugInfo;
            this.addressVersion = engine.AddressVersion;
        }

        public bool TryEvaluate(IVariableManager manager, EvaluateArguments args, [MaybeNullWhen(false)] out EvaluateResponse response)
        {
            var (cast, expression) = ParseCastOperation(args.Expression.AsMemory());
            if (TryInitialEvaluate(expression, out var evalContext))
            {
                while (!evalContext.Expression.IsEmpty)
                {
                    if (!TryRemainingEvaluate(ref evalContext)) break;
                }

                if (!evalContext.Expression.IsEmpty)
                {
                    string expr = new string(evalContext.Expression.Span);
                    response = new EvaluateResponse($"\"{expr}\" expression not implemented", 0)
                        .AsFailedEval();
                    return true;
                }

                return TryCreateResponse(manager, cast, evalContext.Item, evalContext.Type, out response);
            }

            response = null;
            return false;
        }

        bool TryRemainingEvaluate(ref ExpressionEvalContext evalContext)
        {
            var expr = evalContext.Expression;
            if (expr.IsEmpty) return false;

            var item = evalContext.Item;
            var type = evalContext.Type;
            if (expr.Span[0] == '[')
            {
                var bracketIndex = expr.Span.IndexOf(']');
                if (bracketIndex != -1)
                {
                    var keyBuffer = expr.Slice(1, bracketIndex - 1);
                    var remain = expr.Slice(bracketIndex + 1);

                    if (item is Neo.VM.Types.Map)
                    {
                        // TODO: implement map contract type 
                        return false;
                    }
                    else
                    {
                        if (int.TryParse(keyBuffer.Span, out var key))
                        {
                            if (item is NeoArray array
                                && key < array.Count)
                            {
                                ContractType newType = type is StructContractType structType && array.Count == structType.Fields.Count
                                    ? structType.Fields[key].Type : UnspecifiedContractType.Unspecified;

                                evalContext = new ExpressionEvalContext(remain, array[key], newType);
                                return true;
                            }
                            else if (item is Neo.VM.Types.PrimitiveType primitive)
                            {
                                var span = primitive.GetSpan();
                                if (key < span.Length)
                                {
                                    evalContext = new ExpressionEvalContext(remain, new Neo.VM.Types.ByteString(new[] { span[key] }), PrimitiveContractType.ByteArray);
                                    return true;
                                }
                            }
                        }
                    }
                }
            }
            else if (expr.Span[0] == '.'
                && type is StructContractType structType
                && item is Neo.VM.Types.Array array
                && structType.Fields.Count == array.Count)
            {
                expr = expr.Slice(1);
                for (int i = 0; i < structType.Fields.Count; i++)
                {
                    var field = structType.Fields[i];
                    if (expr.StartsWith(field.Name))
                    {
                        expr = expr.Slice(field.Name.Length);
                        evalContext = new ExpressionEvalContext(expr, array[i], field.Type);
                        return true;
                    }
                }
            }

            return false;
        }

        bool TryInitialEvaluate(ReadOnlyMemory<char> expression, [MaybeNullWhen(false)] out ExpressionEvalContext evalContext)
        {
            if (expression.StartsWith("#"))
            {
                if (storageContainer.TryEvaluate(expression, out evalContext)) return true;
                if (TryEvaluateIndexedSlot(expression, ARG_SLOTS_PREFIX, context.Arguments, out evalContext)) return true;
                if (TryEvaluateIndexedSlot(expression, EVAL_STACK_PREFIX, context.EvaluationStack, out evalContext)) return true;
                if (TryEvaluateIndexedSlot(expression, LOCAL_SLOTS_PREFIX, context.LocalVariables, out evalContext)) return true;
                if (TryEvaluateIndexedSlot(expression, RESULT_STACK_PREFIX, resultStack, out evalContext)) return true;
                if (TryEvaluateIndexedSlot(expression, STATIC_SLOTS_PREFIX, context.StaticFields, out evalContext)) return true;
            }
            else if (debugInfo is not null)
            {
                if (TryEvaluateNamedSlot(expression, context.StaticFields, debugInfo.StaticVariables, out evalContext)) return true;

                if (debugInfo.TryGetMethod(context.InstructionPointer, out var method))
                {
                    if (TryEvaluateNamedSlot(expression, context.Arguments, method.Parameters, out evalContext)) return true;
                    if (TryEvaluateNamedSlot(expression, context.LocalVariables, method.Variables, out evalContext)) return true;
                }
            }

            evalContext = default;
            return false;
        }

        static bool IsValidRemaining(ReadOnlyMemory<char> expression) => expression.IsEmpty || expression.Span[0] == '.' || expression.Span[0] == '[';

        static bool TryEvaluateIndexedSlot(ReadOnlyMemory<char> expression, string prefix, IReadOnlyList<StackItem> slot, [MaybeNullWhen(false)] out ExpressionEvalContext context)
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
                        var result = slot[slotIndex];
                        var remaining = expression.Slice(slotIndexExpr.Length);
                        context = new ExpressionEvalContext(remaining, result, UnspecifiedContractType.Unspecified);
                        return true;
                    }
                }
            }

            context = default;
            return false;
        }

        static bool TryEvaluateNamedSlot(ReadOnlyMemory<char> expression, IReadOnlyList<StackItem> slot, IReadOnlyList<DebugInfo.SlotVariable> variables,
            [MaybeNullWhen(false)] out ExpressionEvalContext context)
        {
            for (int i = 0; i < variables.Count; i++)
            {
                var variable = variables[i];
                if (expression.StartsWith(variable.Name) && variable.Index < slot.Count)
                {
                    var remaining = expression.Slice(variable.Name.Length);
                    if (IsValidRemaining(remaining))
                    {
                        // result = (slot[variable.Index], variable.Type);
                        // TODO: return type info
                        context = new ExpressionEvalContext(remaining, slot[variable.Index], UnspecifiedContractType.Unspecified);
                        return true;
                    }
                }
            }

            context = default;
            return false;
        }

        EvaluateResponse AsResponse(IVariableManager manager, StackItem result, ContractType resultType)
        {
            var variable = result.AsVariable(manager, string.Empty, resultType, addressVersion);

            return new EvaluateResponse(variable.Value, variable.VariablesReference)
            {
                Type = resultType.AsTypeName()
            };
        }

        bool TryCast(StackItem item, CastOperation castOp, [MaybeNullWhen(false)] out StackItem result, out ContractType resultType)
        {
            resultType = castOp switch
            {
                CastOperation.Address => PrimitiveContractType.Address,
                CastOperation.Boolean => PrimitiveContractType.Boolean,
                CastOperation.ByteArray => PrimitiveContractType.ByteArray,
                CastOperation.HexString => PrimitiveContractType.String,
                CastOperation.Integer => PrimitiveContractType.Integer,
                CastOperation.String => PrimitiveContractType.String,
                _ => throw new InvalidCastException($"Unexpected Cast Operator {castOp}"),
            };

            try
            {
                if (castOp == CastOperation.Boolean)
                {
                    result = item.GetBoolean();
                    return true;
                }
                if (item.IsNull)
                {
                    result = StackItem.Null;
                    return true;
                }

                switch (castOp)
                {
                    case CastOperation.Integer:
                        result = item.GetInteger();
                        return true;
                    case CastOperation.String:
                        result = item.GetString() ?? StackItem.Null;
                        return true;
                    case CastOperation.HexString:
                        result = Convert.ToHexString(item.GetSpan());
                        return true;
                    case CastOperation.ByteArray:
                        {
                            result = null!;
                            if (item is ByteString byteString)
                            {
                                result = byteString;
                            }
                            if (item is Neo.VM.Types.Buffer buffer)
                            {
                                result = (ByteString)buffer.InnerBuffer;
                            }
                            if (item is Neo.VM.Types.PrimitiveType)
                            {
                                result = (ByteString)item.GetSpan().ToArray();
                            }

                            if (result is not null)
                            {
                                return true;
                            }
                            else  
                            {
                                break;
                            }
                        }
                    case CastOperation.Address:
                    {
                        var span = item is Neo.VM.Types.PrimitiveType || item is Neo.VM.Types.Buffer ? item.GetSpan() : default;
                        if (span.Length == UInt160.Length)
                        {
                            result = item is ByteString byteString
                                ? byteString
                                : item is Neo.VM.Types.Buffer buffer
                                    ? buffer.InnerBuffer
                                    : item.GetSpan().ToArray();
                            return true;
                        }
                    }
                    break;
                }
            }
            catch { }

            result = null;
            return false;
        }

        bool TryCreateResponse(IVariableManager manager, string cast, StackItem item, ContractType type, [MaybeNullWhen(false)] out EvaluateResponse response)
        {
            if (string.IsNullOrEmpty(cast))
            {
                response = AsResponse(manager, item, type);
                return true;
            }

            if (CastOperations.TryGetValue(cast, out var castOp))
            {
                if (castOp == CastOperation.None)
                {
                    response = AsResponse(manager, item, type);
                    return true;
                }

                response = TryCast(item, castOp, out var castResult, out var castResultTime)
                    ? AsResponse(manager, castResult, castResultTime)
                    : new EvaluateResponse($"'{cast}' cast failed", 0).AsFailedEval();
                return true;
            }

            if (cast == "tx")
            {
                response = item is NeoArray array && array.Count == NativeStructs.Transaction.Fields.Count
                    ? AsResponse(manager, item, NativeStructs.Transaction)
                    : new EvaluateResponse($"'tx' cast failed", 0).AsFailedEval();
                return true;
            }

            if (cast == "block")
            {
                response = item is NeoArray array && array.Count == NativeStructs.Block.Fields.Count
                    ? AsResponse(manager, item, NativeStructs.Block)
                    : new EvaluateResponse($"'block' cast failed", 0).AsFailedEval();
                return true;
            }



            response = new EvaluateResponse($"Unknown cast operator {cast}", 0).AsFailedEval();
            return true;
        }

        static (string cast, ReadOnlyMemory<char> expression) ParseCastOperation(ReadOnlyMemory<char> expression)
        {
            if (!expression.IsEmpty && expression.Span[0] == '(')
            {
                var parenIndex = expression.Span.IndexOf(')');
                if (parenIndex != -1)
                {
                    var cast = new string(expression.Slice(1, parenIndex - 1).Span);
                    return (cast, expression.Slice(parenIndex + 1));
                }
            }

            return (string.Empty, expression);
        }
    }
}
