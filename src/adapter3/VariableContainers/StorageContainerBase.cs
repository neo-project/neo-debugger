using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo.SmartContract;

namespace NeoDebug.Neo3
{
    using StackItem = Neo.VM.Types.StackItem;

    internal abstract class StorageContainerBase : IVariableContainer
    {
        protected abstract IEnumerable<(ReadOnlyMemory<byte> key, StorageItem item)> GetStorages();

        public IEnumerable<Variable> Enumerate(IVariableManager manager)
        {
            var storages = GetStorages();
            foreach (var (key, item) in storages)
            {
                var keyHashCode = key.Span.GetSequenceHashCode().ToString("x8");
                var kvp = new KvpContainer(key, item, keyHashCode);
                yield return new Variable()
                {
                    Name = keyHashCode,
                    Value = string.Empty,
                    VariablesReference = manager.Add(kvp),
                    NamedVariables = 2
                };
            }
        }

        public (StackItem? item, ReadOnlyMemory<char> remaining) Evaluate(ReadOnlyMemory<char> expression)
        {
            if (TryGetKeyHash(expression, out var keyHash)
                && TryFindStorage(GetStorages(), keyHash, out var storage))
            {
                var remain = expression.Slice(19);
                if (remain.Length >= 3
                    && remain.Span.Slice(0, 3).SequenceEqual("key"))
                {
                    return (storage.key, remain.Slice(3));
                }
                else if (remain.Length >= 4
                    && remain.Span.Slice(0, 4).SequenceEqual("item"))
                {
                    return (storage.item.Value, remain.Slice(4));
                }
            }

            throw new InvalidOperationException("Invalid storage evaluation");

            static bool TryGetKeyHash(ReadOnlyMemory<char> expression, out int value)
            {
                if (expression.Length >= 18
                    && expression.StartsWith(DebugSession.STORAGE_PREFIX)
                    && expression.Span[8] == '['
                    && expression.Span[17] == ']'
                    && expression.Span[18] == '.'
                    && int.TryParse(expression.Slice(9, 8).Span, NumberStyles.HexNumber, null, out value))
                {
                    return true;
                }

                value = default;
                return false;
            }

            static bool TryFindStorage(IEnumerable<(ReadOnlyMemory<byte> key, StorageItem item)> storages, int hashCode, out (ReadOnlyMemory<byte> key, StorageItem item) storage)
            {
                foreach (var (key, item) in storages)
                {
                    var keyHashCode = key.Span.GetSequenceHashCode();
                    if (hashCode == keyHashCode)
                    {
                        storage = (key, item);
                        return true;
                    }
                }

                storage = default;
                return false;
            }
        }

        private class KvpContainer : IVariableContainer
        {
            private readonly ReadOnlyMemory<byte> key;
            private readonly StorageItem item;
            private readonly string prefix;

            public KvpContainer(ReadOnlyMemory<byte> key, StorageItem item, string hashCode)
            {
                this.key = key;
                this.item = item;
                this.prefix = $"{DebugSession.STORAGE_PREFIX}[{hashCode}].";
            }

            public IEnumerable<Variable> Enumerate(IVariableManager manager)
            {
                var keyItem = ByteArrayContainer.Create(manager, key, "key");
                keyItem.EvaluateName = prefix + "key";
                yield return keyItem;

                var valueItem = ByteArrayContainer.Create(manager, item.Value, "item");
                valueItem.EvaluateName = prefix + "item";
                yield return valueItem;
            }
        }
    }
}
