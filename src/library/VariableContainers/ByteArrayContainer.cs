using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo.VM.Types;
using System;
using System.Collections.Generic;
using System.Text;

namespace NeoDebug.VariableContainers
{
    public class ByteArrayContainer : IVariableContainer
    {
        internal class ValuesContainer : IVariableContainer
        {
            private readonly ReadOnlyMemory<byte> memory;

            public ValuesContainer(ReadOnlyMemory<byte> memory)
            {
                this.memory = memory;
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

        private readonly IVariableContainerSession session;
        private readonly ReadOnlyMemory<byte> memory;

        public ByteArrayContainer(IVariableContainerSession session, ReadOnlyMemory<byte> memory)
        {
            this.session = session;
            this.memory = memory;
        }

        public static Variable GetVariable(ByteArray byteArray, IVariableContainerSession session, string name = null)
        {
            return GetVariable(byteArray.GetByteArray(), session, name);
        }

        public static Variable GetVariable(ReadOnlyMemory<byte> memory, IVariableContainerSession session, string name = null)
        {
            var container = new ByteArrayContainer(session, memory);
            var containerID = session.AddVariableContainer(container);
            return new Variable()
            {
                Name = name,
                Type = $"ByteArray[{memory.Length}]>",
                VariablesReference = containerID,
                NamedVariables = 5
            };
        }

        public IEnumerable<Variable> GetVariables()
        {
            yield return new Variable()
            {
                Name = "<as string>",
                Value = Encoding.UTF8.GetString(memory.Span),
                Type = "String"
            };

            var valuesContainer = new ValuesContainer(memory);
            var valuesContainerID = session.AddVariableContainer(valuesContainer);
            yield return new Variable()
            {
                Name = "<as byte array>",
                Type = "Byte[]",
                VariablesReference = valuesContainerID,
            };

            var bigInt = new System.Numerics.BigInteger(memory.Span);

            yield return new Variable()
            {
                Name = "<as integer>",
                Value = bigInt.ToString(),
                Type = "Integer"
            };

            yield return new Variable()
            {
                Name = "<as hex>",
                Value = "0x" + bigInt.ToString("x"),
                Type = "Integer"
            };

            //yield return new Variable()
            //{
            //    Name = "<as bool>",
            //    Value = array.GetBoolean().ToString(),
            //    Type = "Boolean"
            //};
        }
    }
}
