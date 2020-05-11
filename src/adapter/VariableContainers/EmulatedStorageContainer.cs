using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using NeoDebug.VariableContainers;
using NeoFx;
using System;
using System.Collections.Generic;

namespace NeoDebug
{
    class EmulatedStorageContainer : IVariableContainer
    {
        class KvpContainer : IVariableContainer
        {
            private readonly string hashCode;
            private readonly ReadOnlyMemory<byte> key;
            private readonly ReadOnlyMemory<byte> value;
            private readonly bool constant;

            public KvpContainer(int hashCode, ReadOnlyMemory<byte> key, ReadOnlyMemory<byte> value, bool constant)
            {
                this.hashCode = hashCode.ToString("x");
                this.key = key;
                this.value = value;
                this.constant = constant;
            }

            public IEnumerable<Variable> GetVariables()
            {
                yield return new Variable()
                {
                    Name = "key",
                    Value = key.Span.ToHexString(),
                    EvaluateName = $"$storage[{hashCode}].key",
                };

                yield return new Variable()
                {
                    Name = "value",
                    Value = value.Span.ToHexString(),
                    EvaluateName = $"$storage[{hashCode}].value",
                };

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
                var keyHashCode = key.Span.GetSequenceHashCode();
                yield return new Variable()
                {
                    Name = keyHashCode.ToString("x"),
                    Value = string.Empty,
                    VariablesReference = session.AddVariableContainer(
                        new KvpContainer(keyHashCode, key, item.Value, item.IsConstant)),
                    NamedVariables = 3
                };
            }
        }
    }
}
