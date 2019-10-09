using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo.VM;
using NeoDebug.VariableContainers;
using NeoFx.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace NeoDebug.Adapter.ModelAdapters
{
    class BlockAdapter : AdapterBase, IVariableProvider
    {
        public readonly Block Value;

        public BlockAdapter(in Block value)
        {
            Value = value;
        }

        public static BlockAdapter Create(in Block value)
        {
            return new BlockAdapter(value);
        }

        public bool GetTransactionCount(ExecutionEngine engine)
        {
            engine.CurrentContext.EvaluationStack.Push(Value.Transactions.Length);
            return true;
        }

        public bool GetTransactions(ExecutionEngine engine)
        {
            if (Value.Transactions.Length <= engine.MaxArraySize)
            {
                var items = Value.Transactions.WrapStackItems(TransactionAdapter.Create);
                engine.CurrentContext.EvaluationStack.Push(items);
                return true;
            }
            return false;
        }

        public static bool GetTransaction(ExecutionEngine engine)
        {
            var evalStack = engine.CurrentContext.EvaluationStack;
            if (evalStack.TryPopAdapter<BlockAdapter>(out var adapter))
            {
                var index = (int)evalStack.Pop().GetBigInteger();
                if (index >= 0 && index < adapter.Value.Transactions.Length)
                {
                    var item = new TransactionAdapter(adapter.Value.Transactions.Span[index]);
                    evalStack.Push(item);
                    return true;
                }
            }
            return false;
        }

        public Variable GetVariable(IVariableContainerSession session)
        {
            throw new NotImplementedException();
        }
    }
}
