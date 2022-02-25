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

        public static Variable Create(IVariableManager manager, ReadOnlyMemory<byte> buffer, string name, string typeName = "byte")
        {
            if (buffer.Length == 1)
            {
                return new Variable()
                {
                    Name = name,
                    Value = "0x" + buffer.Span[0].ToString("x"),
                    Type = "Byte"
                };
            }

            var container = new ByteArrayContainer(buffer);
            return new Variable()
            {
                Name = name,
                Value = $"{typeName}[{buffer.Length}]",
                VariablesReference = manager.Add(container),
                IndexedVariables = buffer.Length,
            };
        }

        public static bool TryCreate(IVariableManager manager, StackItem item, string name, [MaybeNullWhen(false)] out Variable variable)
        {
            if (item is Buffer buffer)
            {
                variable = ByteArrayContainer.Create(manager, buffer.InnerBuffer, name, nameof(Buffer));
                return true;
            }
            if (item is ByteString byteString)
            {
                variable = ByteArrayContainer.Create(manager, byteString, name, nameof(ByteString));
                return true;
            }
            if (item is Neo.VM.Types.PrimitiveType primitive)
            {
                byteString = (ByteString)item.ConvertTo(StackItemType.ByteString);
                variable = ByteArrayContainer.Create(manager, byteString, name);
                return true;
            }

            variable = default;
            return false;
        }

        public static Variable Create(IVariableManager manager, StackItem item, string name)
        {
            if (TryCreate(manager, item, name, out var variable))
            {
                return variable;
            }
            
            throw new NotSupportedException($"cannot create ByteArrayContainer for {item.Type}");
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
