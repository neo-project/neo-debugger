using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo.VM;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;

namespace Neo.DebugAdapter
{
    internal class EmulatedStorageContainer : IVariableContainer
    {
        internal class KvpContainer : IVariableContainer
        {
            private readonly NeoDebugSession session;
            private readonly byte[] key;
            private readonly byte[] value;
            private readonly bool constant;

            public KvpContainer(NeoDebugSession session, (byte[] key, byte[] value, bool constant) kvp)
            {
                this.session = session;
                key = kvp.key;
                value = kvp.value;
                constant = kvp.constant;
            }

            public IEnumerable<Variable> GetVariables(VariablesArguments args)
            {
                yield return ByteArrayContainer.GetVariable(key, session, "key");
                yield return ByteArrayContainer.GetVariable(value, session, "value");
                yield return new Variable()
                {
                    Name = "constant",
                    Value = constant.ToString(),
                    Type = "Boolean"
                };
            }
        }

        private readonly NeoDebugSession session;
        private readonly EmulatedStorage storage;

        public EmulatedStorageContainer(NeoDebugSession session, EmulatedStorage storage)
        {
            this.storage = storage;
            this.session = session;
        }

        public IEnumerable<Variable> GetVariables(VariablesArguments args)
        {
            foreach (var kvp in storage.Storage)
            {
                yield return new Variable()
                {
                    Name = "0x" + new BigInteger(kvp.Value.key).ToString("x"),
                    VariablesReference = session.AddVariableContainer(
                        new KvpContainer(session, kvp.Value)),
                    NamedVariables = 3
                };
            }
        }
    }
}
