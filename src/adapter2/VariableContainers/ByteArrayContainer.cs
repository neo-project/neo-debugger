using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo.VM.Types;
using System;
using System.Collections.Generic;

namespace NeoDebug.VariableContainers
{
    public class ByteArrayContainer : IVariableContainer
    {
        private readonly ReadOnlyMemory<byte> memory;

        public ByteArrayContainer(ReadOnlyMemory<byte> memory)
        {
            this.memory = memory;
        }

        public static Variable Create(IVariableContainerSession session, ByteArray byteArray, string? name, bool hashed = false)
        {
            return Create(session, byteArray.GetByteArray(), name);
        }

        public ReadOnlySpan<byte> Span => memory.Span;

        public static Variable Create(IVariableContainerSession session, ReadOnlyMemory<byte> memory, string? name, bool hashed = false)
        {
            var container = new ByteArrayContainer(memory);
            var containerID = session.AddVariableContainer(container);
            var hash = hashed ? "#" : string.Empty;

            return new Variable()
            {
                Name = name,
                Type = $"{hash}ByteArray[{memory.Length}]",
                Value = container.Span.ToHexString(),
                VariablesReference = containerID,
                IndexedVariables = memory.Length,
            };
        }

        public IEnumerable<Variable> GetVariables()
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
