using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;

namespace NeoDebug.Neo3
{
    using StackItem = Neo.VM.Types.StackItem;
    using StackItemType = Neo.VM.Types.StackItemType;
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

        public static Variable Create(IVariableManager manager, StackItem item, string name)
        {
            var variable = item switch
            {
                Buffer buffer => ByteArrayContainer.Create(manager, buffer.InnerBuffer, name),
                ByteString byteString => ByteArrayContainer.Create(manager, byteString, name),
                Neo.VM.Types.PrimitiveType primitive => ByteArrayContainer.Create(manager, (ByteString)item.ConvertTo(StackItemType.ByteString), name),
                _ => throw new NotSupportedException($"{item.Type}"),
            };
            variable.Type = $"{item.Type}";
            return variable;
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
