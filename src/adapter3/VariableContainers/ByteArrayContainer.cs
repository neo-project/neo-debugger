using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;

namespace NeoDebug.Neo3
{
    using ByteString = Neo.VM.Types.ByteString;
    using Buffer = Neo.VM.Types.Buffer;

    class ByteArrayContainer : IVariableContainer
    {
        private readonly ReadOnlyMemory<byte> memory;

        public ReadOnlyMemory<byte> Memory => memory;

        public ByteArrayContainer(ReadOnlyMemory<byte> memory)
        {
            this.memory = memory;
        }

        // public static bool TryCreate(Neo.VM.Types.StackItem item, [MaybeNullWhen(false)] out ByteArrayContainer container)
        // {
        //     if (item is Neo.VM.Types.ByteString byteString)
        //     {
        //         container = new ByteArrayContainer(byteString);
        //         return true;
        //     }
        //     if (item is Neo.VM.Types.Buffer buffer)
        //     {
        //         container = new ByteArrayContainer(buffer.InnerBuffer);
        //         return true;
        //     }

        //     if (item is Neo.VM.Types.PrimitiveType)
        //     {
        //         try
        //         {
        //             byteString = (ByteString)item.ConvertTo(Neo.VM.Types.StackItemType.ByteString);
        //             container = new ByteArrayContainer(byteString);
        //             return true;
        //         }
        //         catch { }
        //     }

        //     container = default;
        //     return false;
        // }

        public static Variable Create(IVariableManager manager, ReadOnlyMemory<byte> buffer, string name)
        {
            var container = new ByteArrayContainer(buffer);
            return new Variable()
            {
                Name = name,
                Value = $"byte[{buffer.Length}]",
                VariablesReference = manager.Add(container),
                IndexedVariables = buffer.Length,
            };
        }

        public IEnumerable<Variable> Enumerate(IVariableManager manager)
        {
            for (int i = 0; i < memory.Length; i++)
            {
                yield return new Variable()
                {
                    Name = i.ToString(),
                    Value = "0x" + memory.Span[i].ToString("x"),
                    Type = "Byte"
                };
            }
        }
    }
}
