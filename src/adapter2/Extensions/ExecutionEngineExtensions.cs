using System;
using System.Diagnostics.CodeAnalysis;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo.VM;
using Neo.VM.Types;
using NeoDebug.VariableContainers;
using NeoFx;

namespace NeoDebug
{
    static class ExecutionEngineExtensions
    {
        public static bool TryPopInterface<T>(this RandomAccessStack<StackItem> stack, [NotNullWhen(true)] out T? value)
            where T : class
        {
            if (stack.Pop() is InteropInterface @interface)
            {
                var t = @interface.GetInterface<T>();
                if (t != null)
                {
                    value = t;
                    return true;
                }
            }

            value = default;
            return false;
        }

        public static bool TryPopAdapter<T>(this RandomAccessStack<StackItem> stack, [NotNullWhen(true)] out T? adapter)
            where T : ModelAdapters.AdapterBase
        {
            if (stack.Pop() is T _adapter)
            {
                adapter = _adapter;
                return true;
            }
            adapter = null;
            return false;
        }

        public static bool TryAdapterOperation<T>(this ExecutionEngine engine, Func<T, bool> func)
            where T : ModelAdapters.AdapterBase
        {
            if (engine.CurrentContext.EvaluationStack.TryPopAdapter<T>(out var adapter))
            {
                return func(adapter);
            }
            return false;
        }

        public static bool TryToArray(this UInt160 uInt160, [NotNullWhen(true)] out byte[]? value)
        {
            var buffer = new byte[UInt160.Size];

            if (uInt160.TryWrite(buffer))
            {
                value = buffer;
                return true;
            }

            value = default;
            return false;
        }

        public static bool TryToArray(this UInt256 uInt256, [NotNullWhen(true)] out byte[]? value)
        {
            var buffer = new byte[UInt256.Size];

            if (uInt256.TryWrite(buffer))
            {
                value = buffer;
                return true;
            }

            value = default;
            return false;
        }

        public static void EmitAppCall(this ScriptBuilder @this, UInt160 scriptHash, bool useTailCall = false)
        {
            if (scriptHash.TryToArray(out var array))
            {
                @this.EmitAppCall(array, useTailCall);
            }
            else
            {
                throw new Exception("UInt160.TryToArray failed");
            }
        }

        public delegate StackItem WrapStackItem<T>(in T item);

        public static StackItem[] WrapStackItems<T>(this ReadOnlyMemory<T> memory, WrapStackItem<T> wrapItem)
        {
            var items = new StackItem[memory.Length];
            for (int i = 0; i < memory.Length; i++)
            {
                items[i] = wrapItem(memory.Span[i]);
            }
            return items;
        }

        public static Variable GetVariable(this StackItem item, IVariableContainerSession session, string name, string? typeHint = null)
        {
            switch (typeHint)
            {
                case "Boolean":
                    return new Variable()
                    {
                        Name = name,
                        Value = item.GetBoolean().ToString(),
                        Type = "#Boolean",
                    };
                case "Integer":
                    return new Variable()
                    {
                        Name = name,
                        Value = item.GetBigInteger().ToString(),
                        Type = "#Integer",
                    };
                case "String":
                    return new Variable()
                    {
                        Name = name,
                        Value = item.GetString(),
                        Type = "#String",
                    };
                case "HexString":
                    return new Variable()
                    {
                        Name = name,
                        Value = item.GetBigInteger().ToHexString(),
                        Type = "#ByteArray"
                    };
                case "ByteArray":
                    return ByteArrayContainer.Create(session, item.GetByteArray(), name, true);
            }

            return item switch
            {
                IVariableProvider provider => provider.GetVariable(session, name),
                Neo.VM.Types.Boolean _ => new Variable()
                {
                    Name = name,
                    Value = item.GetBoolean().ToString(),
                    Type = "Boolean"
                },
                Neo.VM.Types.Integer _ => new Variable()
                {
                    Name = name,
                    Value = item.GetBigInteger().ToString(),
                    Type = "Integer"
                },
                Neo.VM.Types.ByteArray byteArray => ByteArrayContainer.Create(session, byteArray, name),
                Neo.VM.Types.InteropInterface _ => new Variable()
                {
                    Name = name,
                    Type = "InteropInterface",
                    Value = string.Empty
                },
                Neo.VM.Types.Map map => NeoMapContainer.Create(session, map, name),
                // NeoArrayContainer.Create will detect Struct (which inherits from Array)
                // and distinguish accordingly
                Neo.VM.Types.Array array => NeoArrayContainer.Create(session, array, name),
                _ => throw new NotImplementedException($"GetStackItemValue {item.GetType().FullName}"),
            };
        }
    }
}
