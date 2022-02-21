using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo;
using Neo.BlockchainToolkit;
using Neo.SmartContract;
using OneOf;

namespace NeoDebug.Neo3
{
    using StackItem = Neo.VM.Types.StackItem;
    using StorageValueTypeDef = OneOf<ContractParameterType, StructDef>;

    internal abstract class StorageContainerBase : IVariableContainer
    {
        private readonly ContractStorageSchema? schema;

        protected StorageContainerBase(ContractStorageSchema? schema)
        {
            this.schema = schema;
        }

        protected abstract IEnumerable<(ReadOnlyMemory<byte> key, StorageItem item)> GetStorages();

        static string ToString(StorageValueTypeDef typeDef) => typeDef.Match(cpt => $"{cpt}", sd => sd.Name);

        public IEnumerable<Variable> Enumerate(IVariableManager manager)
        {
            var storages = GetStorages();

            if (schema is not null)
            {
                foreach (var storageDef in schema.StorageDefs)
                {
                    yield return SchematizedStorageContainer.Create(manager, storageDef, storages);
                }
            }
            else
            {
                foreach (var (key, item) in storages)
                {
                    var kvp = new KvpContainer(key, item);
                    yield return new Variable()
                    {
                        Name = key.Span.ToHexString(),
                        Value = string.Empty,
                        VariablesReference = manager.Add(kvp),
                        NamedVariables = 2
                    };
                }
            }
        }

        public (StackItem? item, ReadOnlyMemory<char> remaining) Evaluate(ReadOnlyMemory<char> expression)
        {
            if (TryGetKey(expression, out var key, out var remainder))
            {
                if (remainder.Span.StartsWith(".key"))
                {
                    return (key, remainder.Slice(4));
                }
                else if (remainder.Span.StartsWith(".item"))
                {
                    if (TryFindStorageItem(GetStorages(), key, out var item))
                    {
                        return (item.Value, remainder.Slice(5));
                    }
                }
            }

            throw new InvalidOperationException("Invalid storage evaluation");

            static bool TryGetKey(ReadOnlyMemory<char> expression, out ReadOnlyMemory<byte> key, out ReadOnlyMemory<char> remainder)
            {
                remainder = default;
                key = default;
                if (!expression.StartsWith($"{DebugSession.STORAGE_PREFIX}[")) return false;
                expression = expression.Slice(DebugSession.STORAGE_PREFIX.Length + 1);
                var bracketIndex = expression.Span.IndexOf(']');
                if (bracketIndex == -1) return false;
                remainder = expression.Slice(bracketIndex + 1);
                expression = expression.Slice(0, bracketIndex);
                if (expression.Span.StartsWith("0x")) expression = expression.Slice(2);
                try
                {
                    key = Convert.FromHexString(expression.Span);
                    return true;
                }
                catch { return false; }
            }

            static bool TryFindStorageItem(IEnumerable<(ReadOnlyMemory<byte> key, StorageItem item)> storages, ReadOnlyMemory<byte> key, [MaybeNullWhen(false)] out StorageItem item)
            {
                foreach (var storage in storages)
                {
                    if (key.Span.SequenceEqual(storage.key.Span))
                    {
                        item = storage.item;
                        return true;
                    }
                }

                item = default;
                return false;
            }
        }

        private class KvpContainer : IVariableContainer
        {
            private readonly ReadOnlyMemory<byte> key;
            private readonly StorageItem item;
            private readonly string prefix;

            public KvpContainer(ReadOnlyMemory<byte> key, StorageItem item)
            {
                this.key = key;
                this.item = item;
                this.prefix = $"{DebugSession.STORAGE_PREFIX}[0x{key.Span.ToHexString()}].";
            }

            public IEnumerable<Variable> Enumerate(IVariableManager manager)
            {
                var keyItem = ByteArrayContainer.Create(manager, key, "key");
                keyItem.EvaluateName = prefix + "key";
                yield return keyItem;

                var valueItem = ByteArrayContainer.Create(manager, new ReadOnlyMemory<byte>(item.Value), "item");
                valueItem.EvaluateName = prefix + "item";
                yield return valueItem;
            }
        }
    }
}
