using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo;
using Neo.BlockchainToolkit;
using Neo.SmartContract;

namespace NeoDebug.Neo3
{
    using Storages = IEnumerable<(ReadOnlyMemory<byte> key, StorageItem item)>;
    using StoragesList = IReadOnlyList<(ReadOnlyMemory<byte> key, StorageItem item)>;

    internal class SchematizedStorageContainer : IVariableContainer
    {
        readonly StorageDef storageDef;
        readonly StoragesList storages;

        public SchematizedStorageContainer(StorageDef storageDef, StoragesList storages)
        {
            this.storageDef = storageDef;
            this.storages = storages;
        }

        public static Variable Create(IVariableManager manager, StorageDef storageDef, Storages storages)
        {
            if (storageDef.KeySegments.Count == 0)
            {
                var (_, item) = storages.SingleOrDefault(s => s.key.Span.SequenceEqual(storageDef.KeyPrefix.Span));
                return item.AsVariable(manager, storageDef.Name, storageDef.Value);
            }
            else
            {
                var values = storages.Where(s => s.key.Span.StartsWith(storageDef.KeyPrefix.Span)).ToArray();
                var container = new SchematizedStorageContainer(storageDef, values);

                var keyType = string.Join(", ", storageDef.KeySegments.Select(s => $"{s.Name}: {s.Type}"));
                return new Variable()
                {
                    Name = storageDef.Name,
                    Value = $"({keyType}) -> {storageDef.Value.AsString()}[{values.Length}]",
                    VariablesReference = manager.Add(container),
                    IndexedVariables = values.Length,
                };
            }
        }

        public IEnumerable<Variable> Enumerate(IVariableManager manager)
        {
            for (int i = 0; i < storages.Count; i++)
            {
                var (key, item) = storages[i];
                var segments = key.AsKeySegments(storageDef).ToArray();

                if (segments.Length == 1)
                {
                    var name = segments[0].AsString();
                    yield return item.AsVariable(manager, name, storageDef.Value);
                }
                else
                {
                    var container = new SchematizedStorageItemContainer(storageDef, key, item);
                    yield return new Variable()
                    {
                        Name = $"{i}",
                        Value = string.Empty,
                        VariablesReference = manager.Add(container),
                        NamedVariables = 2,
                    };

                }
            }
        }
    }
}
