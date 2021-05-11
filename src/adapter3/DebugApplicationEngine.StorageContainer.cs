using System;
using System.Collections.Generic;
using System.Linq;
using Neo;
using Neo.Persistence;
using Neo.SmartContract;

namespace NeoDebug.Neo3
{
    internal partial class DebugApplicationEngine
    {
        private class StorageContainer : StorageContainerBase
        {
            private readonly DataCache snapshot;
            private readonly int? contractId;

            public StorageContainer(UInt160 scriptHash, DataCache snapshot)
            {
                this.snapshot = snapshot;
                contractId = Neo.SmartContract.Native.NativeContract.ContractManagement.GetContract(snapshot, scriptHash)?.Id;
            }

            protected override IEnumerable<(ReadOnlyMemory<byte>, StorageItem)> GetStorages()
            {
                return contractId.HasValue
                    ? snapshot.Find(StorageKey.CreateSearchPrefix(contractId.Value, default))
                        .Select(t => ((ReadOnlyMemory<byte>)t.Key.Key, t.Value))
                    : Enumerable.Empty<(ReadOnlyMemory<byte>, StorageItem)>();
            }
        }
    }
}
