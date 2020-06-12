using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo;
using Neo.Ledger;
using Neo.Persistence;

namespace NeoDebug.Neo3
{
    class StorageContainer : IVariableContainer
    {
        private readonly StoreView store;
        private readonly int? contractId;

        public StorageContainer(UInt160 scriptHash, StoreView store)
        {
            this.store = store;
            contractId = store.Contracts.TryGet(scriptHash)?.Id;
        }

        public IEnumerable<Variable> Enumerate(IVariableManager manager)
        {
            if (contractId.HasValue)
            {
                foreach (var (key, item) in store.Storages.Find())
                {
                    if (key.Id != contractId.Value)
                        continue;

                    var keyHashCode = key.Key.GetSequenceHashCode().ToString("x");
                    var kvp = new KvpContainer(key, item, keyHashCode);
                    yield return new Variable()
                    {
                        Name = keyHashCode,
                        Value = string.Empty,
                        VariablesReference = manager.Add(kvp),
                        NamedVariables = 3
                    };
                }
            }
        }

        class KvpContainer : IVariableContainer
        {
            private readonly StorageKey key;
            private readonly StorageItem item;
            private readonly string hashCode;

            public KvpContainer(StorageKey key, StorageItem item, string hashCode)
            {
                this.key = key;
                this.item = item;
                this.hashCode = hashCode;
            }

            public IEnumerable<Variable> Enumerate(IVariableManager manager)
            {
                var keyVariable = ByteArrayContainer.Create(manager, key.Key, "key");
                keyVariable.EvaluateName = $"#storage[{hashCode}].key";
                yield return keyVariable;

                var itemVariable = ByteArrayContainer.Create(manager, item.Value, "item");
                itemVariable.EvaluateName = $"#storage[{hashCode}].value";
                yield return itemVariable;

                yield return new Variable()
                {
                    Name = "isConstant",
                    EvaluateName = $"#storage[{hashCode}].isConstant",
                    Type = "Boolean",
                    Value = item.IsConstant.ToString(),
                };
            }
        }
    }
}
