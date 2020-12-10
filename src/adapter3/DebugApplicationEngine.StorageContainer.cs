using System;
using System.Collections.Generic;
using Neo.Persistence;
using Neo;
using System.Linq;
using Neo.Ledger;

namespace NeoDebug.Neo3
{
    internal partial class DebugApplicationEngine
    {
        private class StorageContainer : StorageContainerBase
        {
            private readonly StoreView store;
            private readonly int? contractId;

            public StorageContainer(UInt160 scriptHash, StoreView store)
            {
                this.store = store;
                contractId = Neo.SmartContract.Native.NativeContract.Management.GetContract(store, scriptHash)?.Id;
            }

            protected override IEnumerable<(ReadOnlyMemory<byte>, StorageItem)> GetStorages()
            {
                return contractId.HasValue
                    ? store.Storages.Find(StorageKey.CreateSearchPrefix(contractId.Value, default))
                        .Select(t => ((ReadOnlyMemory<byte>)t.Key.Key, t.Value))
                    : Enumerable.Empty<(ReadOnlyMemory<byte>, StorageItem)>();
            }
        }
    }
}
