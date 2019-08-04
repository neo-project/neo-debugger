using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo.VM;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Neo.DebugAdapter
{
    internal class EmulatedStorage
    {
        private class StorageContext
        {
            public byte[] ScriptHash;

            public int GetHashCode(ReadOnlySpan<byte> key)
            {
                var keyHash = Crypto.GetHashCode(key);
                var contextHash = Crypto.GetHashCode<byte>(ScriptHash.AsSpan());
                return HashCode.Combine(keyHash, contextHash);
            }
        }

        private readonly Dictionary<int, byte[]> storage = new Dictionary<int, byte[]>();

        static bool TryGetStorageContext(RandomAccessStack<StackItem> evalStack, out StorageContext context)
        {
            if (evalStack.Pop() is VM.Types.InteropInterface interop)
            {
                context = interop.GetInterface<StorageContext>();
                return true;
            }

            context = default;
            return false;
        }

        public bool Delete (ExecutionEngine engine)
        {
            var evalStack = engine.CurrentContext.EvaluationStack;
            if (TryGetStorageContext(evalStack, out var ctx))
            {
                var key = evalStack.Pop().GetByteArray();
                var storageHash = ctx.GetHashCode(key);

                storage.Remove(storageHash);
                return true;
            }

            return false;
        }

        public bool Put(ExecutionEngine engine)
        {
            var evalStack = engine.CurrentContext.EvaluationStack;
            if (TryGetStorageContext(evalStack, out var ctx))
            {
                var key = evalStack.Pop().GetByteArray();
                var storageHash = ctx.GetHashCode(key);

                var value = evalStack.Pop().GetByteArray();
                storage.Add(storageHash, value);
                return true;
            }

            return false;
        }

        public bool Get(ExecutionEngine engine)
        {
            var evalStack = engine.CurrentContext.EvaluationStack;
            if (TryGetStorageContext(evalStack, out var context))
            {
                var key = evalStack.Pop().GetByteArray();
                var storageHash = context.GetHashCode(key);

                if (storage.TryGetValue(storageHash, out var value))
                {
                    evalStack.Push(value);
                }
                else
                {
                    evalStack.Push(new byte[0]);
                }

                return true;
            }

            return false;
        }

        public bool GetContext(ExecutionEngine engine)
        {
            var context = new StorageContext()
            {
                ScriptHash = engine.CurrentContext.ScriptHash,
            };

            engine.CurrentContext.EvaluationStack.Push(StackItem.FromInterface(context));
            return true;
        }
    }
}
