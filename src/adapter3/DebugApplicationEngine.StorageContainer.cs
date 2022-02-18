using System;
using System.Collections.Generic;
using System.Linq;
using Neo;
using Neo.BlockchainToolkit;
using Neo.Persistence;
using Neo.SmartContract;
using Neo.SmartContract.Native;

namespace NeoDebug.Neo3
{
    internal partial class DebugApplicationEngine
    {
        private class StorageContainer : StorageContainerBase
        {
            private readonly DataCache snapshot;
            private readonly int? contractId;

            public StorageContainer(UInt160 scriptHash, DataCache snapshot, IReadOnlyDictionary<UInt160, ContractStorageSchema> schemaMap) 
                : this(scriptHash, snapshot, schemaMap.TryGetValue(scriptHash, out var schema) ? schema : null)
            {
            }

            public StorageContainer(UInt160 scriptHash, DataCache snapshot, ContractStorageSchema? schema) : base(schema)
            {
                this.snapshot = snapshot;
                this.contractId = NativeContract.ContractManagement.GetContract(snapshot, scriptHash)?.Id;
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
