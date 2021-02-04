using System;
using System.Collections.Generic;
using Neo.SmartContract;

namespace NeoDebug.Neo3
{
    internal sealed partial class TraceApplicationEngine
    {
        private class StorageContainer : StorageContainerBase
        {
            public readonly IEnumerable<(ReadOnlyMemory<byte>, StorageItem)> storages;

            public StorageContainer(IEnumerable<(ReadOnlyMemory<byte>, StorageItem)> storages)
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
