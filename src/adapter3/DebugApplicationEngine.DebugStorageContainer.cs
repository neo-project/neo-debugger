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
        private class DebugStorageContainer : StorageContainer
        {
            private readonly StoreView store;
            private readonly int? contractId;

            public DebugStorageContainer(UInt160 scriptHash, StoreView store)
            {
                this.store = store;
                contractId = store.Contracts.TryGet(scriptHash)?.Id;
            }

            protected override IEnumerable<(ReadOnlyMemory<byte>, StorageItem)> GetStorages()
            {
                return contractId.HasValue
                    ? store.Storages.Find(CreateSearchPrefix(contractId.Value, default))
                        .Select(t => ((ReadOnlyMemory<byte>)t.Key.Key, t.Value))
                    : Enumerable.Empty<(ReadOnlyMemory<byte>, StorageItem)>();

                // TODO: use StorageKey.CreateSearchPrefix in preview 4
                //       https://github.com/neo-project/neo/pull/1824
                static byte[] CreateSearchPrefix(int id, ReadOnlySpan<byte> prefix)
                {
                    byte[] buffer = new byte[sizeof(int) + prefix.Length];
                    System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(buffer, id);
                    prefix.CopyTo(buffer.AsSpan(sizeof(int)));
                    return buffer;
                }
            }
        }
    }
}
