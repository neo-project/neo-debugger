using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo.BlockchainToolkit;
using Neo.SmartContract;

namespace NeoDebug.Neo3
{
    internal class SchematizedStorageItemContainer : IVariableContainer
    {
        readonly StorageDef storageDef;
        readonly ReadOnlyMemory<byte> key;
        readonly StorageItem item;

        public SchematizedStorageItemContainer(StorageDef storageDef, ReadOnlyMemory<byte> key, StorageItem item)
        {
            if (storageDef.KeySegments.Count <= 0) throw new ArgumentException(nameof(storageDef));

            this.storageDef = storageDef;
            this.key = key;
            this.item = item;
        }

        public IEnumerable<Variable> Enumerate(IVariableManager manager)
        {
            var keySegments = key.AsKeySegments(storageDef).ToArray();
            var keyContainer = new SchematizedKeyContainer(keySegments);

            yield return new Variable()
            {
                Name = "key",
                Value = string.Empty,
                VariablesReference = manager.Add(keyContainer),
                NamedVariables = keySegments.Length
            };

            yield return item.AsVariable(manager, "value", storageDef.Value);
        }
    }
}
