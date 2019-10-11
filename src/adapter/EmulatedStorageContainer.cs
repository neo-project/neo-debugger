using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using NeoDebug.VariableContainers;
using NeoFx;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace NeoDebug.Adapter
{
    internal class EmulatedStorageContainer : IVariableContainer
    {
        internal class KvpContainer : IVariableContainer
        {
            private readonly IVariableContainerSession session;
            private readonly ReadOnlyMemory<byte> key;
            private readonly ReadOnlyMemory<byte> value;
            private readonly bool constant;

            public KvpContainer(IVariableContainerSession session, ReadOnlyMemory<byte> key, ReadOnlyMemory<byte> value, bool constant)
            {
                this.session = session;
                this.key = key;
                this.value = value;
                this.constant = constant;
            }

            public IEnumerable<Variable> GetVariables()
            {
                // TODO remove .ToArray() calls
                yield return ByteArrayContainer.GetVariable(key.ToArray(), session, "key");
                yield return ByteArrayContainer.GetVariable(value.ToArray(), session, "value");
                yield return new Variable()
                {
                    Name = "constant",
                    Value = constant.ToString(),
                    Type = "Boolean"
                };
            }
        }

        private readonly UInt160 scriptHash;
        private readonly IVariableContainerSession session;
        private readonly EmulatedStorage storage;

        public EmulatedStorageContainer(IVariableContainerSession session, UInt160 scriptHash, EmulatedStorage storage)
        {
            this.session = session;
            this.scriptHash = scriptHash;
            this.storage = storage;
        }

        public IEnumerable<Variable> GetVariables()
        {
            foreach (var (key, item) in storage.EnumerateStorage(scriptHash))
            {
                yield return new Variable()
                {
                    Name = "0x" + new BigInteger(key.Span).ToString("x"),
                    VariablesReference = session.AddVariableContainer(
                        new KvpContainer(session, key, item.Value, item.IsConstant)),
                    NamedVariables = 3
                };
            }
        }
    }
}
