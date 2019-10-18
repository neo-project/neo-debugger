using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo.VM.Types;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace NeoDebug.VariableContainers
{
    public class ByteArrayContainer : IVariableContainer
    {
        private readonly IVariableContainerSession session;
        private readonly ReadOnlyMemory<byte> memory;

        public ByteArrayContainer(IVariableContainerSession session, ReadOnlyMemory<byte> memory)
        {
            this.session = session;
            this.memory = memory;
        }

        public static Variable Create(IVariableContainerSession session, ByteArray byteArray, string? name, bool hashed = false)
        {
            return Create(session, byteArray.GetByteArray(), name);
        }

        public BigInteger AsBigInteger()
        {
            return new BigInteger(memory.Span);
        }

        public static Variable Create(IVariableContainerSession session, ReadOnlyMemory<byte> memory, string? name, bool hashed = false)
        {
            var container = new ByteArrayContainer(session, memory);
            var containerID = session.AddVariableContainer(container);
            var hash = hashed ? "#" : string.Empty;

            return new Variable()
            {
                Name = name,
                Type = $"{hash}ByteArray[{memory.Length}]",
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
