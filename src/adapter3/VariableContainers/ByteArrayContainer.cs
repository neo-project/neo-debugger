using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;

namespace NeoDebug.Neo3
{
    using ByteString = Neo.VM.Types.ByteString;
    using Buffer = Neo.VM.Types.Buffer;

    class ByteArrayContainer : IVariableContainer
    {
        private readonly ReadOnlyMemory<byte> memory;

        ByteArrayContainer(ReadOnlyMemory<byte> memory)
        {
            this.memory = memory;
        }

        public static Variable Create(IVariableManager manager, ByteString byteString, string name)
        {
            // TODO: byteString.Memory should be public to avoid copy
            return Create(manager, byteString.Span.ToArray(), name, "ByteString");
        }

        public static Variable Create(IVariableManager manager, Buffer buffer, string name)
        {
            return Create(manager, buffer.InnerBuffer, name, "Buffer");
        }

        static Variable Create(IVariableManager manager, ReadOnlyMemory<byte> memory, string name, string type)
        {
            var container = new ByteArrayContainer(memory);
            return new Variable()
            {
                Name = name,
                Value = $"{type}[{memory.Length}]",
                VariablesReference = manager.Add(container),
                IndexedVariables = memory.Length,
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
