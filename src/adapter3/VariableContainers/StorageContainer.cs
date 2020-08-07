using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo;
using Neo.Ledger;
using Neo.Persistence;

namespace NeoDebug.Neo3
{
    internal class StorageContainer : IStorageContainer
    {
        private readonly StoreView store;
        private readonly int? contractId;

        public StorageContainer(UInt160 scriptHash, StoreView store)
        {
            this.store = store;
            contractId = store.Contracts.TryGet(scriptHash)?.Id;
        }

        bool TryFind(int hashCode, out (StorageKey key, StorageItem item) tuple)
        {
            if (contractId.HasValue)
            {
                foreach (var (key, item) in store.Storages.Find())
                {
                    if (key.Id != contractId.Value)
                        continue;

                    var keyHashCode = key.Key.GetSequenceHashCode();
                    if (hashCode == keyHashCode)
                    {
                        tuple = (key, item);
                        return true;
                    }
                }
            }

            tuple = default;
            return false;
        }

        public IEnumerable<Variable> Enumerate(IVariableManager manager)
        {
            if (contractId.HasValue)
            {
                var storages = store.Storages.Find(CreateSearchPrefix(contractId.Value, default));
                foreach (var (key, item) in storages)
                {
                    var keyHashCode = key.Key.GetSequenceHashCode().ToString("x");
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

            // TODO: PR Opened to make StorageKey.CreateSearchPrefix public
            //       If accepted, remove this copy of that method 
            //       https://github.com/neo-project/neo/pull/1824
            static byte[] CreateSearchPrefix(int id, ReadOnlySpan<byte> prefix)
            {
                byte[] buffer = new byte[sizeof(int) + prefix.Length];
                BinaryPrimitives.WriteInt32LittleEndian(buffer, id);
                prefix.CopyTo(buffer.AsSpan(sizeof(int)));
                return buffer;
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
                    return tuple.key.Key;
                }
                else if (remain.Span.SequenceEqual("item"))
                {
                    return tuple.item.Value;
                }
                else if (remain.Span.SequenceEqual("isConstant"))
                {
                    return tuple.item.IsConstant;
                }
            }

            return null;
        }

        class KvpContainer : IVariableContainer
        {
            private readonly StorageKey key;
            private readonly StorageItem item;
            private readonly string hashCode;

            public KvpContainer(StorageKey key, StorageItem item, string hashCode)
            {
                this.key = key;
                this.item = item;
                this.hashCode = hashCode;
            }

            public IEnumerable<Variable> Enumerate(IVariableManager manager)
            {
                yield return ForEvaluation(key.Key.ToVariable(manager, "key"));
                yield return ForEvaluation(item.Value.ToVariable(manager, "item"));
                yield return ForEvaluation(item.IsConstant.ToVariable(manager, "isConstant"));

                Variable ForEvaluation(Variable variable)
                {
                    variable.EvaluateName = $"#storage[{hashCode}].{variable.Name}";
                    return variable;
                }
            }
        }
    }
}
