using System;
using System.Collections.Generic;
using Neo.BlockchainToolkit.Models;
using Neo.SmartContract;

namespace NeoDebug.Neo3
{
    internal sealed partial class TraceApplicationEngine
    {
        private class StorageContainer : StorageContainerBase
        {
            public readonly IEnumerable<(ReadOnlyMemory<byte>, StorageItem)> storages;

            public StorageContainer(IEnumerable<(ReadOnlyMemory<byte>, StorageItem)> storages, IReadOnlyList<StorageDef> storageDefs, byte addressVersion, StorageView storageView)
                : base(storageDefs, addressVersion, storageView)
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
