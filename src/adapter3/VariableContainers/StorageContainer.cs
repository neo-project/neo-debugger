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

                    var keyHashCode = key.Key.GetSequenceHashCode();
                    var kvp = new KvpContainer(key, item);
                    yield return new Variable()
                    {
                        Name = keyHashCode.ToString("x"),
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

            public KvpContainer(StorageKey key, StorageItem item)
            {
                this.key = key;
                this.item = item;
            }

            public IEnumerable<Variable> Enumerate(IVariableManager manager)
            {
                yield return new Variable()
                {
                    Name = "key",
                    Value = key.Key.ToHexString(),
                    // EvaluateName = $"$storage[{hashCode}].key",
                };

                yield return new Variable()
                {
                    Name = "value",
                    Value = item.Value.ToHexString(),
                    // EvaluateName = $"$storage[{hashCode}].value",
                };

                yield return new Variable()
                {
                    Name = "isConstant",
                    Value = item.IsConstant.ToString(),
                    Type = "Boolean"
                };
            }
        }
    }
}
