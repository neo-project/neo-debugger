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
        internal class ValuesContainer : IVariableContainer
        {
            private readonly byte[] array;

            public ValuesContainer(byte[] array)
            {
                this.array = array;
            }

            public IEnumerable<Variable> GetVariables(VariablesArguments args)
            {
                for (int i = 0; i < array.Length; i++)
                {
                    yield return new Variable()
                    {
                        Name = i.ToString(),
                        Value = "0x" + array[i].ToString("x"),
                        Type = "Byte"
                    };
                }
            }
        }

        private readonly NeoDebugSession session;
        private readonly ByteArray array;

        public ByteArrayContainer(NeoDebugSession session, ByteArray array)
        {
            this.session = session;
            this.array = array;
        }

        public static Variable GetVariable(byte[] byteArray, NeoDebugSession session, string name = null)
        {
            return GetVariable(new ByteArray(byteArray), session, name);
        }

        public static Variable GetVariable(ByteArray byteArray, NeoDebugSession session, string name = null)
        {
            var container = new ByteArrayContainer(session, byteArray);
            var containerID = session.AddVariableContainer(container);
            return new Variable()
            {
                Name = name,
                Type = $"ByteArray[{byteArray.GetByteArray().Length}]>",
                VariablesReference = containerID,
                NamedVariables = 4
            };
        }

        public IEnumerable<Variable> GetVariables(VariablesArguments args)
        {
            yield return new Variable()
            {
                Name = "<as string>",
                Value = array.GetString(),
                Type = "String"
            };

            var valuesContainer = new ValuesContainer(array.GetByteArray());
            var valuesContainerID = session.AddVariableContainer(valuesContainer);
            yield return new Variable()
            {
                Name = "<as byte array>",
                Type = "Byte[]",
                VariablesReference = valuesContainerID,
            };

            yield return new Variable()
            {
                Name = "<as integer>",
                Value = array.GetBigInteger().ToString(),
                Type = "Integer"
            };

            yield return new Variable()
            {
                Name = "<as bool>",
                Value = array.GetBoolean().ToString(),
                Type = "Boolean"
            };
        }

        //    public IEnumerable<Variable> GetVariables(VariablesArguments args)
        //{
        //    if (args.Filter == VariablesArguments.FilterValue.Indexed)
        //    {
        //        var byteArray = array.GetByteArray();
        //        for (int i = 0; i < byteArray.Length; i++)
        //        {
        //            yield return new Variable()
        //            {
        //                Name = i.ToString(),
        //                Value = "0x" + byteArray[i].ToString("x"),
        //                Type = "Byte"
        //            };
        //        }
        //    }

        //    if (args.Filter == VariablesArguments.FilterValue.Named)
        //    {
        //        var container = new AsContainer(array);
        //        var containerID = session.AddVariableContainer(container);

        //        yield return new Variable()
        //        {
        //            Name = "<as>",
        //            VariablesReference = containerID
        //        };
        //    }
        //}
    }
}
