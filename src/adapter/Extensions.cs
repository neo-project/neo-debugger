using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using Neo.VM;
using Neo.VM.Types;
using NeoFx;

namespace NeoDebug
{
    static class Extensions
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

        public static ulong ReadVarInt(this BinaryReader reader, ulong max = ulong.MaxValue)
        {
            byte fb = reader.ReadByte();
            ulong value;
            if (fb == 0xFD)
                value = reader.ReadUInt16();
            else if (fb == 0xFE)
                value = reader.ReadUInt32();
            else if (fb == 0xFF)
                value = reader.ReadUInt64();
            else
                value = fb;
            if (value > max) throw new FormatException();
            return value;
        }

        public static void WriteVarInt(this BinaryWriter writer, long value)
        {
            if (value < 0)
                throw new ArgumentOutOfRangeException();
            if (value < 0xFD)
            {
                writer.Write((byte)value);
            }
            else if (value <= 0xFFFF)
            {
                writer.Write((byte)0xFD);
                writer.Write((ushort)value);
            }
            else if (value <= 0xFFFFFFFF)
            {
                writer.Write((byte)0xFE);
                writer.Write((uint)value);
            }
            else
            {
                writer.Write((byte)0xFF);
                writer.Write(value);
            }
        }

        public static byte[] ReadVarBytes(this BinaryReader reader, int max = 0x1000000)
        {
            return reader.ReadBytes((int)reader.ReadVarInt((ulong)max));
        }

        public static void WriteVarBytes(this BinaryWriter writer, byte[] value)
        {
            writer.WriteVarInt(value.Length);
            writer.Write(value);
        }
    }
}
