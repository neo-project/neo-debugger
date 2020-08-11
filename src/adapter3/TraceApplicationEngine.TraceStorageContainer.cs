using System;
using System.Collections.Generic;
using Neo.Ledger;

namespace NeoDebug.Neo3
{
    internal sealed partial class TraceApplicationEngine
    {
        private class TraceStorageContainer : StorageContainer
        {
            public readonly IEnumerable<(ReadOnlyMemory<byte>, StorageItem)> storages;

            public TraceStorageContainer(IEnumerable<(ReadOnlyMemory<byte>, StorageItem)> storages)
            {
                this.storages = storages;
            }

            protected override IEnumerable<(ReadOnlyMemory<byte>, StorageItem)> GetStorages()
            {
                return storages;
            }
        }
    }
}
