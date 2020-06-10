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
            var container = new ByteArrayContainer(byteString);
            return ToVariable(manager, container, name, "ByteString");
        }

        public static Variable Create(IVariableManager manager, Buffer buffer, string name)
        {
            var container = new ByteArrayContainer(buffer.InnerBuffer);
            return ToVariable(manager, container, name, "Buffer");
        }

        static Variable ToVariable(IVariableManager manager, ByteArrayContainer container, string name, string type)
        {
            return new Variable()
            {
                Name = name,
                Value = $"{type}[{container.memory.Length}]",
                VariablesReference = manager.Add(container),
                IndexedVariables = container.memory.Length,
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
