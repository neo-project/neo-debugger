using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo.SmartContract;
using Neo.VM.Types;

namespace NeoDebug.Neo3
{
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
                    NamedVariables = 3
                };
            }
        }

        public Neo.VM.Types.StackItem? Evaluate(ReadOnlyMemory<char> expression)
        {
            bool TryGetKeyHash(out int value)
            {
                if (expression.Length >= 18
                    && expression.StartsWith("#storage[")
                    && expression.Span[17] == ']'
                    && expression.Span[18] == '.'
                    && int.TryParse(expression.Slice(9, 8).Span, NumberStyles.HexNumber, null, out value))
                {
                    return true;
                }

                value = default;
                return false;
            }

            if (TryGetKeyHash(out var keyHash)
                && TryFind(keyHash, out var tuple))
            {
                var remain = expression.Slice(19);
                if (remain.Span.SequenceEqual("key"))
                {
                    return tuple.key;
                }
                else if (remain.Span.SequenceEqual("item"))
                {
                    return tuple.item.Value;
                }
            }

            return null;

            bool TryFind(int hashCode, out (ReadOnlyMemory<byte> key, StorageItem item) tuple)
            {
                foreach (var (key, item) in GetStorages())
                {
                    var keyHashCode = key.Span.GetSequenceHashCode();
                    if (hashCode == keyHashCode)
                    {
                        tuple = (key, item);
                        return true;
                    }
                }

                tuple = default;
                return false;
            }
        }

        private class KvpContainer : IVariableContainer
        {
            private readonly ReadOnlyMemory<byte> key;
            private readonly StorageItem item;
            private readonly string hashCode;

            public KvpContainer(ReadOnlyMemory<byte> key, StorageItem item, string hashCode)
            {
                this.key = key;
                this.item = item;
                this.hashCode = hashCode;
            }

            public IEnumerable<Variable> Enumerate(IVariableManager manager)
            {
                var keyItem = ByteArrayContainer.Create(manager, key, "key");
                keyItem.EvaluateName = $"#storage[{hashCode}].key";
                yield return keyItem;

                var valueItem = ByteArrayContainer.Create(manager, item.Value, "item");
                valueItem.EvaluateName = $"#storage[{hashCode}].item";
                yield return valueItem;
            }
        }
    }
}
