using Neo.VM;
using NeoDebug.VariableContainers;
using System;
using System.Diagnostics.CodeAnalysis;

#nullable enable

namespace NeoDebug.Adapter
{
    internal partial class InteropService
    {
        [Flags]
        private enum StorageFlags : byte
        {
            None = 0,
            Constant = 0x01
        }

        private class StorageContext
        {
            public byte[] ScriptHash { get; }
            public bool ReadOnly { get; }

            public StorageContext(byte[] scriptHash, bool readOnly)
            {
                ScriptHash = scriptHash;
                ReadOnly = readOnly;
            }

            public int GetHashCode(ReadOnlySpan<byte> key)
            {
                return GetHashCode(ScriptHash, key);
            }

            public static int GetHashCode(ReadOnlySpan<byte> scriptHash, ReadOnlySpan<byte> key)
            {
                return HashCode.Combine(Crypto.GetHashCode(key), Crypto.GetHashCode<byte>(scriptHash));
            }
        }

        public void RegisterStorage(Action<string, Func<ExecutionEngine, bool>, int> register)
        {
            register("Neo.Storage.GetContext", Storage_GetContext, 1);
            register("Neo.Storage.GetReadOnlyContext", Storage_GetReadOnlyContext, 1);
            register("Neo.Storage.Get", Storage_Get, 100);
            register("Neo.Storage.Put", Storage_Put, 0);
            register("Neo.Storage.Delete", Storage_Delete, 100);
            register("Neo.Storage.Find", Storage_Find, 1);
            register("Neo.StorageContext.AsReadOnly", StorageContext_AsReadOnly, 1);

            register("System.Storage.GetContext", Storage_GetContext, 1);
            register("System.Storage.GetReadOnlyContext", Storage_GetReadOnlyContext, 1);
            register("System.Storage.Get", Storage_Get, 100);
            register("System.Storage.Put", Storage_Put, 0);
            register("System.Storage.PutEx", Storage_PutEx, 0);
            register("System.Storage.Delete", Storage_Delete, 100);
            register("System.StorageContext.AsReadOnly", StorageContext_AsReadOnly, 1);

            register("AntShares.Storage.GetContext", Storage_GetContext, 1);
            register("AntShares.Storage.Get", Storage_Get, 100);
            register("AntShares.Storage.Put", Storage_Put, 0);
            register("AntShares.Storage.Delete", Storage_Delete, 100);
        }

        public IVariableContainer GetStorageContainer(IVariableContainerSession session)
        {
            return new EmulatedStorageContainer(session, storage);
        }

        private static bool TryGetStorageContext(RandomAccessStack<StackItem> evalStack, [NotNullWhen(true)] out StorageContext? context)
        {
            if (evalStack.Pop() is Neo.VM.Types.InteropInterface interop)
            {
                context = interop.GetInterface<StorageContext>();
                return true;
            }

            context = default;
            return false;
        }

        private bool Storage_GetContext(ExecutionEngine engine)
        {
            var storageContext = new StorageContext(engine.CurrentContext.ScriptHash, false);
            engine.CurrentContext.EvaluationStack.Push(StackItem.FromInterface(storageContext));
            return true;
        }

        private bool Storage_GetReadOnlyContext(ExecutionEngine engine)
        {
            var storageContext = new StorageContext(engine.CurrentContext.ScriptHash, true);
            engine.CurrentContext.EvaluationStack.Push(StackItem.FromInterface(storageContext));
            return true;
        }

        private bool StorageContext_AsReadOnly(ExecutionEngine engine)
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

        private bool Storage_Get(ExecutionEngine engine)
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
                    evalStack.Push(Array.Empty<byte>());
                }

                return true;
            }

            return false;
        }

        private bool PutHelper(StorageContext storageContext, byte[] key, byte[] value, bool constant = false)
        {
            if (trigger != TriggerType.Application) return false;
            if (key.Length > 1024) return false;
            if (storageContext.ReadOnly) return false;
            // TODO: CheckStorageContext

            var storageHash = storageContext.GetHashCode(key);
            if (storage.TryGetValue(storageHash, out var currentValue)
                && currentValue.constant)
            {
                return false;
            }

            storage[storageHash] = (key, value, constant);
            return true;
        }

        private bool Storage_Put(ExecutionEngine engine)
        {
            var evalStack = engine.CurrentContext.EvaluationStack;
            if (TryGetStorageContext(evalStack, out var storageContext))
            {
                var key = evalStack.Pop().GetByteArray();
                var value = evalStack.Pop().GetByteArray();
                return PutHelper(storageContext, key, value);
            }

            return false;
        }

        private bool Storage_PutEx(ExecutionEngine engine)
        {
            var evalStack = engine.CurrentContext.EvaluationStack;
            if (TryGetStorageContext(evalStack, out var storageContext))
            {
                var key = evalStack.Pop().GetByteArray();
                var value = evalStack.Pop().GetByteArray();
                var storageFlags = (StorageFlags)(byte)evalStack.Pop().GetBigInteger();
                return PutHelper(storageContext, key, value, (storageFlags & StorageFlags.Constant) != 0);
            }

            return false;
        }

        private bool Storage_Delete(ExecutionEngine engine)
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

        private bool Storage_Find(ExecutionEngine engine)
        {
            throw new NotImplementedException(nameof(Storage_Find));
        }
    }
}
