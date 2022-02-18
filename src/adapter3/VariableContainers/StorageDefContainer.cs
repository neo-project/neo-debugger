using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo.BlockchainToolkit;
using Neo.SmartContract;
using OneOf;

namespace NeoDebug.Neo3
{
    internal class StorageDefContainer : IVariableContainer
    {
        readonly StorageDef storageDef;
        readonly IReadOnlyList<(ReadOnlyMemory<byte> key, StorageItem item)> storages;
        readonly IReadOnlyDictionary<string, StructDef> structMap;

        public StorageDefContainer(StorageDef storageDef, IReadOnlyList<(ReadOnlyMemory<byte> key, StorageItem item)> storages, IReadOnlyDictionary<string, StructDef> structMap)
        {
            this.storageDef = storageDef;
            this.storages = storages;
            this.structMap = structMap;
        }

        public static Variable Create(StorageDef storageDef, StorageItem? storageItem, IReadOnlyDictionary<string, StructDef> structMap)
        {
            var value = storageItem is null
                ? "<no value stored>"
                : storageDef.Value.ConvertStorageItem(storageItem, structMap);
            return new Variable()
            {
                Name = storageDef.Name,
                Value = value,
                Type = storageDef.Value.AsString()
            };
        }

        public static Variable Create(IVariableManager manager, StorageDef storageDef, IEnumerable<(ReadOnlyMemory<byte> key, StorageItem item)> storages, IReadOnlyDictionary<string, StructDef> structMap)
        {
            var container = new StorageDefContainer(storageDef, storages.ToArray(), structMap);

            return new Variable()
            {
                IndexedVariables = container.storages.Count,
                Name = storageDef.Name,
                Value = $"{storageDef.Value.AsString()}[{container.storages.Count}]",
                VariablesReference = manager.Add(container),
            };
        }

        public IEnumerable<Variable> Enumerate(IVariableManager manager)
        {
            foreach (var storage in storages)
            {
                yield return new Variable()
                {
                    Name = Convert.ToHexString(storage.key.Span),
                    Value = Convert.ToHexString(storage.item.Value)
                };
            }
        }
    }
}
