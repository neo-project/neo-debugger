using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo.VM;
using NeoDebug.VariableContainers;
using NeoFx;
using NeoFx.Models;
using System;
using System.Collections.Generic;

namespace NeoDebug
{
    partial class InteropService
    {
        private class StorageContext : ModelAdapters.AdapterBase, IVariableProvider
        {
            public readonly UInt160 ScriptHash;
            public readonly bool ReadOnly;

            public StorageContext(UInt160 scriptHash, bool readOnly)
            {
                ScriptHash = scriptHash;
                ReadOnly = readOnly;
            }

            public Variable GetVariable(IVariableContainerSession session, string name)
            {
                return new Variable()
                {
                    Name = name,
                    Type = "StorageContext",
                    Value = ScriptHash.ToString(),
                };
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

        private bool Storage_GetContext(ExecutionEngine engine)
        {
            var scriptHash = new UInt160(engine.CurrentContext.ScriptHash);
            var storageContext = new StorageContext(scriptHash, false);
            engine.CurrentContext.EvaluationStack.Push(storageContext);
            return true;
        }

        private bool Storage_GetReadOnlyContext(ExecutionEngine engine)
        {
            var scriptHash = new UInt160(engine.CurrentContext.ScriptHash);
            var storageContext = new StorageContext(scriptHash, true);
            engine.CurrentContext.EvaluationStack.Push(storageContext);
            return true;
        }

        private bool StorageContext_AsReadOnly(ExecutionEngine engine)
        {
            var evalStack = engine.CurrentContext.EvaluationStack;
            if (evalStack.TryPopAdapter<StorageContext>(out var context))
            {
                evalStack.Push(context.ReadOnly
                    ? context
                    : new StorageContext(context.ScriptHash, true));
                return true;
            }

            return false;
        }

        private bool Storage_Get(ExecutionEngine engine)
        {
            var evalStack = engine.CurrentContext.EvaluationStack;
            if (evalStack.TryPopAdapter<StorageContext>(out var context))
            {
                var storageKey = new StorageKey(context.ScriptHash, evalStack.Pop().GetByteArray());

                if (storage.TryGetStorage(storageKey, out var storageItem))
                {
                    evalStack.Push(storageItem.Value.ToArray());
                }
                else
                {
                    evalStack.Push(Array.Empty<byte>());
                }

                return true;
            }
            return false;
        }

        private bool TryPut(StorageContext storageContext, ReadOnlyMemory<byte> key, ReadOnlyMemory<byte> value, bool constant)
        {
            if (trigger != TriggerType.Application) return false;
            if (key.Length > 1024) return false;
            if (storageContext.ReadOnly) return false;
            // TODO: CHeckStorageCOntext

            return storage.TryPut(new StorageKey(storageContext.ScriptHash, key), value, constant);
        }

        private bool Storage_Put(ExecutionEngine engine)
        {
            var evalStack = engine.CurrentContext.EvaluationStack;
            if (evalStack.TryPopAdapter<StorageContext>(out var context))
            {
                var key = evalStack.Pop().GetByteArray();
                var value = evalStack.Pop().GetByteArray();

                return TryPut(context, key, value, false);
            }
            return false;
        }

        private bool Storage_PutEx(ExecutionEngine engine)
        {
            var evalStack = engine.CurrentContext.EvaluationStack;
            if (evalStack.TryPopAdapter<StorageContext>(out var context))
            {
                var key = evalStack.Pop().GetByteArray();
                var value = evalStack.Pop().GetByteArray();
                var constant = (((byte)evalStack.Pop().GetBigInteger()) & 0x01) != 0; // 0x01 == StorageFlags.Constant

                return TryPut(context, key, value, constant);
            }
            return false;
        }

        private bool Storage_Delete(ExecutionEngine engine)
        {
            var evalStack = engine.CurrentContext.EvaluationStack;
            if (evalStack.TryPopAdapter<StorageContext>(out var context))
            {
                var key = evalStack.Pop().GetByteArray();
                if (trigger != TriggerType.Application) return false;
                if (key.Length > 1024) return false;
                if (context.ReadOnly) return false;

                return storage.TryDelete(new StorageKey(context.ScriptHash, key));
            }
            return false;
        }

        private bool Storage_Find(ExecutionEngine engine)
        {
            IEnumerator<(StackItem, StackItem)> EnumerateStorage(UInt160 scriptHash, ReadOnlyMemory<byte> prefix)
            {
                foreach (var (key, item) in storage.EnumerateStorage(scriptHash))
                {
                    if (prefix.Span.SequenceEqual(key.Span.Slice(0, prefix.Length)))
                    {
                        yield return (key.ToArray(), item.Value.ToArray());
                    }
                }
            }

            var evalStack = engine.CurrentContext.EvaluationStack;
            if (evalStack.TryPopAdapter<StorageContext>(out var context))
            {
                var prefix = evalStack.Pop().GetByteArray();
                var iterator = new Iterator(EnumerateStorage(context.ScriptHash, prefix));
                evalStack.Push(StackItem.FromInterface(iterator));
            }
            return false;
        }
    }
}
