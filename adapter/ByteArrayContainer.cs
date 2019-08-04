using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo.VM;
using Neo.VM.Types;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Neo.DebugAdapter
{
    internal class ByteArrayContainer : IVariableContainer
    {
        internal class AsContainer : IVariableContainer
        {
            private readonly Neo.VM.Types.ByteArray array;

            public AsContainer(ByteArray array)
            {
                this.array = array;
            }

            public IEnumerable<Variable> GetVariables(VariablesArguments args)
            {
                yield return new Variable()
                {
                    Name = "<as bool>",
                    Value = array.GetBoolean().ToString(),
                    Type = "Boolean"
                };

                yield return new Variable()
                {
                    Name = "<as integer>",
                    Value = array.GetBigInteger().ToString(),
                    Type = "Integer"
                };

                yield return new Variable()
                {
                    Name = "<as string>",
                    Value = array.GetString(),
                    Type = "String"
                };
            }
        }

        private readonly NeoDebugSession session;
        private readonly Neo.VM.Types.ByteArray array;

        public ByteArrayContainer(NeoDebugSession session, ByteArray array)
        {
            this.session = session;
            this.array = array;
        }

        public IEnumerable<Variable> GetVariables(VariablesArguments args)
        {
            if (args.Filter == VariablesArguments.FilterValue.Indexed)
            {
                var byteArray = array.GetByteArray();
                for (int i = 0; i < byteArray.Length; i++)
                {
                    yield return new Variable()
                    {
                        Name = i.ToString(),
                        Value = "0x" + byteArray[i].ToString("x"),
                        Type = "Byte"
                    };
                }
            }

            if (args.Filter == VariablesArguments.FilterValue.Named)
            {
                var container = new AsContainer(array);
                var containerId = session.AddVariableContainer(container);

                yield return new Variable()
                {
                    Name = "<as>",
                    VariablesReference = containerId
                };
            }
        }
    }
}
