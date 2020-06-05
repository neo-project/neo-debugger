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

        public ByteArrayContainer(ReadOnlyMemory<byte> memory)
        {
            this.memory = memory;
        }

        static Variable Create(IVariableManager manager, ReadOnlyMemory<byte> memory, string name, string type)
        {
            var container = new ByteArrayContainer(memory);
            var containerID = manager.Add(container);
            return new Variable()
            {
                Name = name,
                Type = $"{type}[{memory.Length}]",
                Value = string.Empty,
                VariablesReference = containerID,
                IndexedVariables = memory.Length,
            };
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


        // public static Variable Create(IVariableContainerSession session, ByteArray byteArray, string? name, bool hashed = false)
        // {
        //     return Create(session, byteArray.GetByteArray(), name);
        // }

        // public ReadOnlySpan<byte> Span => memory.Span;

        // public static Variable Create(IVariableContainerSession session, ReadOnlyMemory<byte> memory, string? name, bool hashed = false)
        // {
        //     var container = new ByteArrayContainer(memory);
        //     var containerID = session.AddVariableContainer(container);
        //     var hash = hashed ? "#" : string.Empty;

        //     return new Variable()
        //     {
        //         Name = name,
        //         Type = $"{hash}ByteArray[{memory.Length}]",
        //         Value = container.Span.ToHexString(),
        //         VariablesReference = containerID,
        //         IndexedVariables = memory.Length,
        //     };
        // }

        public IEnumerable<Variable> Enumerate()
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
