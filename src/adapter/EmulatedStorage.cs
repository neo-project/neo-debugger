﻿using Neo.VM;
using NeoDebug.VariableContainers;
using NeoFx;
using NeoFx.Models;
using NeoFx.Storage;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace NeoDebug
{
    internal class EmulatedStorage
    {
        private class StorageKeyEqualityComparer : IEqualityComparer<StorageKey>
        {
            public bool Equals([AllowNull] StorageKey x, [AllowNull] StorageKey y)
            {
                return x.ScriptHash.Equals(y.ScriptHash)
                    && x.Key.Span.SequenceEqual(y.Key.Span);
            }

            public int GetHashCode([DisallowNull] StorageKey obj)
            {
                var hashCode = obj.ScriptHash.GetHashCode();
                for (int i = 0; i < obj.Key.Length; i++)
                {
                    hashCode = HashCode.Combine(hashCode, obj.Key.Span[i]);
                }
                return hashCode;
            }
        }

        private readonly IBlockchainStorage? blockchain;

        private readonly Dictionary<StorageKey, (bool deleted, StorageItem item)> storage =
            new Dictionary<StorageKey, (bool, StorageItem)>(new StorageKeyEqualityComparer());

        public EmulatedStorage(IBlockchainStorage? blockchain)
        {
            this.blockchain = blockchain;
        }

        public IEnumerable<(ReadOnlyMemory<byte> key, StorageItem item)> EnumerateStorage(UInt160 scriptHash)
        {
            foreach (var kvp in storage)
            {
                if (kvp.Key.ScriptHash.Equals(scriptHash) && !kvp.Value.deleted)
                {
                    yield return (kvp.Key.Key, kvp.Value.item);
                }
            }

            if (blockchain != null)
            {
                foreach (var (key, item) in blockchain.EnumerateStorage(scriptHash))
                {
                    var storageKey = new StorageKey(scriptHash, key);
                    if (!storage.ContainsKey(storageKey))
                    {
                        yield return (key, item);
                    }
                }
            }
        }

        public bool TryGetStorage(in StorageKey key, out StorageItem value)
        {
            if (storage.TryGetValue(key, out var item))
            {
                if (!item.deleted)
                {
                    value = item.item;
                    return true;
                }
                else
                {
                    value = default;
                    return false;
                }
            }

            if (blockchain != null)
            {
                return blockchain.TryGetStorage(key, out value);
            }

            value = default;
            return false;
        }

        public bool TryPut(in StorageKey key, ReadOnlyMemory<byte> value, bool constant)
        {
            if (TryGetStorage(key, out var item)
                && item.IsConstant)
            {
                return false;
            }

            storage[key] = (false, new StorageItem(value, constant));
            return true;
        }

        public bool TryDelete(in StorageKey key)
        {
            if (TryGetStorage(key, out var item)
                && item.IsConstant)
            {
                return false;
            }

            storage[key] = (true, default);
            return true;
        }
    }
}
