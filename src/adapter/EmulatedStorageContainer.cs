using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using NeoDebug.VariableContainers;
using System.Collections.Generic;
using System.Numerics;

namespace NeoDebug.Adapter
{
    internal class EmulatedStorageContainer : IVariableContainer
    {
        internal class KvpContainer : IVariableContainer
        {
            private readonly IVariableContainerSession session;
            private readonly byte[] key;
            private readonly byte[] value;
            private readonly bool constant;

            public KvpContainer(IVariableContainerSession session, (byte[] key, byte[] value, bool constant) kvp)
            {
                this.session = session;
                key = kvp.key;
                value = kvp.value;
                constant = kvp.constant;
            }

            public IEnumerable<Variable> GetVariables()
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

        private readonly IVariableContainerSession session;
        private readonly IReadOnlyDictionary<int, (byte[] key, byte[] value, bool constant)> storage;

        public EmulatedStorageContainer(IVariableContainerSession session, IReadOnlyDictionary<int, (byte[] key, byte[] value, bool constant)> storage)
        {
            this.session = session;
            this.storage = storage;
        }

        public IEnumerable<Variable> GetVariables()
        {
            foreach (var kvp in storage)
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
