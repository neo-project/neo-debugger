using Neo.VM;
using NeoDebug.VariableContainers;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NeoDebug.Adapter
{
    internal class EmulatedStorage
    {
        private class StorageContext
        {
            public byte[] ScriptHash { get; }
            public bool ReadOnly { get; }

            public StorageContext(byte[] scriptHash, bool readOnly = true)
            {
                ScriptHash = scriptHash;
                ReadOnly = readOnly;
            }

            public int GetHashCode(ReadOnlySpan<byte> key)
            {
                var keyHash = Crypto.GetHashCode(key);
                var contextHash = Crypto.GetHashCode<byte>(ScriptHash.AsSpan());
                return HashCode.Combine(keyHash, contextHash);
            }
        }

        [Flags]
        private enum StorageFlags : byte
        {
            None = 0,
            Constant = 0x01
        }

        private readonly Dictionary<int, (byte[] key, byte[] value, bool constant)> storage =
            new Dictionary<int, (byte[] key, byte[] value, bool constant)>();

        public IReadOnlyDictionary<int, (byte[] key, byte[] value, bool constant)> Storage => storage;

        public EmulatedStorage(byte[] scriptHash, IEnumerable<(byte[] key, byte[] value, bool constant)> items = null)
        {
            var storageContext = new StorageContext(scriptHash, false);
            foreach (var item in items ?? Enumerable.Empty<(byte[], byte[], bool)>())
            {
                var storageHash = storageContext.GetHashCode(item.key);
                storage[storageHash] = item;
            }
        }

        public IVariableContainer GetStorageContainer(IVariableContainerSession session)
        {
            return new EmulatedStorageContainer(session, this);
        }

        public void RegisterServices(Action<string, Func<ExecutionEngine, bool>> register)
        {
            register("AntShares.Storage.GetContext", GetContext);
            register("AntShares.Storage.Get", Get);
            register("AntShares.Storage.Put", Put);
            register("AntShares.Storage.Delete", Delete);

            register("Neo.Storage.GetContext", GetContext);
            register("Neo.Storage.GetReadOnlyContext", GetReadOnlyContext);
            register("Neo.Storage.Get", Get);
            register("Neo.Storage.Put", Put);
            register("Neo.Storage.Delete", Delete);
            //register("Neo.Storage.Find", Find);

            register("System.Storage.GetContext", GetContext);
            register("System.Storage.GetReadOnlyContext", GetReadOnlyContext);
            register("System.Storage.Get", Get);
            register("System.Storage.Put", Put);
            register("System.Storage.PutEx", PutEx);
            register("System.Storage.Delete", Delete);
            register("System.StorageContext.AsReadOnly", StorageContextAsReadOnly);
        }

        private bool GetContext(ExecutionEngine engine)
        {
            var storageContext = new StorageContext(engine.CurrentContext.ScriptHash, false);
            engine.CurrentContext.EvaluationStack.Push(StackItem.FromInterface(storageContext));
            return true;
        }

        private bool GetReadOnlyContext(ExecutionEngine engine)
        {
            var storageContext = new StorageContext(engine.CurrentContext.ScriptHash, true);
            engine.CurrentContext.EvaluationStack.Push(StackItem.FromInterface(storageContext));
            return true;
        }

        private bool StorageContextAsReadOnly(ExecutionEngine engine)
        {
            var evalStack = engine.CurrentContext.EvaluationStack;
            if (TryGetStorageContext(evalStack, out var storageContext))
            {
                var readOnlyStorageContext = new StorageContext(storageContext.ScriptHash, true);
                evalStack.Push(StackItem.FromInterface(readOnlyStorageContext));
                return true;
            }

            return false;
        }

        private static bool TryGetStorageContext(RandomAccessStack<StackItem> evalStack, out StorageContext context)
        {
            if (evalStack.Pop() is Neo.VM.Types.InteropInterface interop)
            {
                context = interop.GetInterface<StorageContext>();
                return true;
            }

            context = default;
            return false;
        }

        private bool Get(ExecutionEngine engine)
        {
            var evalStack = engine.CurrentContext.EvaluationStack;
            if (TryGetStorageContext(evalStack, out var storageContext))
            {
                var key = evalStack.Pop().GetByteArray();
                var storageHash = storageContext.GetHashCode(key);

                if (storage.TryGetValue(storageHash, out var kvp))
                {
                    evalStack.Push(kvp.value);
                }
                else
                {
                    evalStack.Push(new byte[0]);
                }

                return true;
            }

            return false;
        }

        private bool PutWorkhorse(StorageContext storageContext, byte[] key, byte[] value, bool constant = false)
        {
            // check storage context
            if (key.Length > 1024) return false;
            if (storageContext.ReadOnly) return false;

            var storageHash = storageContext.GetHashCode(key);
            if (storage.TryGetValue(storageHash, out var currentValue) 
                && currentValue.constant)
            {
                return false;
            }

            storage[storageHash] = (key, value, constant);
            return true;
        }

        private bool Put(ExecutionEngine engine)
        {
            var evalStack = engine.CurrentContext.EvaluationStack;
            if (TryGetStorageContext(evalStack, out var storageContext))
            {
                var key = evalStack.Pop().GetByteArray();
                var value = evalStack.Pop().GetByteArray();

                return PutWorkhorse(storageContext, key, value);
            }

            return false;
        }

        private bool PutEx(ExecutionEngine engine)
        {
            var evalStack = engine.CurrentContext.EvaluationStack;
            if (TryGetStorageContext(evalStack, out var storageContext))
            {
                if (storageContext.ReadOnly) return false;
                var key = evalStack.Pop().GetByteArray();
                var value = evalStack.Pop().GetByteArray();
                var storageFlags = (StorageFlags)(byte)evalStack.Pop().GetBigInteger();

                return PutWorkhorse(storageContext, key, value, (storageFlags & StorageFlags.Constant) != 0);
            }

            return false;
        }

        private bool Delete(ExecutionEngine engine)
        {
            var evalStack = engine.CurrentContext.EvaluationStack;
            if (TryGetStorageContext(evalStack, out var storageContext))
            {
                if (storageContext.ReadOnly) return false;
                // check storage context
                var key = evalStack.Pop().GetByteArray();
                var storageHash = storageContext.GetHashCode(key);

                if (storage.TryGetValue(storageHash, out var currentValue)
                    && currentValue.constant)
                {
                    return false;
                }

                storage.Remove(storageHash);
                return true;
            }

            return false;
        }
    }
}
