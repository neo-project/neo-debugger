using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Neo.VM;
using Neo.VM.Types;
using NeoFx;

#nullable enable

namespace NeoDebug.Adapter
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

        public static bool TryPopContainedStruct<T>(this RandomAccessStack<StackItem> stack, out T value)
            where T : struct
        {
            if (stack.Pop() is InteropInterface @interface)
            {
                var t = @interface.GetInterface<StructContainer<T>>();
                if (t != null)
                {
                    value = t.Item;
                    return true;
                }
            }

            value = default;
            return false;
        }

        public static bool TryToArray(this UInt160 uInt160, [NotNullWhen(true)] out byte[]? value)
        {
            var buffer = new byte[UInt160.Size];

            if (uInt160.TryWriteBytes(buffer))
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

            if (uInt256.TryWriteBytes(buffer))
            {
                value = buffer;
                return true;
            }

            value = default;
            return false;
        }

        public static StackItem[] WrapStackItems<T>(this ReadOnlyMemory<T> memory)
            where T : struct
        {
            var items = new StackItem[memory.Length];
            for (int i = 0; i < memory.Length; i++)
            {
                items[i] = StackItem.FromInterface(new StructContainer<T>(memory.Span[i]));
            }
            return items;
        }

        public static void WriteVarInt(this System.IO.BinaryWriter writer, long value)
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

        public static void WriteVarBytes(this System.IO.BinaryWriter writer, byte[] value)
        {
            writer.WriteVarInt(value.Length);
            writer.Write(value);
        }
    }
}
