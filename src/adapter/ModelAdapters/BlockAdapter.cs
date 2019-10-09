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

        public bool GetTransaction(ExecutionEngine engine)
        {
            var evalStack = engine.CurrentContext.EvaluationStack;
            var index = (int)evalStack.Pop().GetBigInteger();
            if (index >= 0 && index < Value.Transactions.Length)
            {
                var item = new TransactionAdapter(Value.Transactions.Span[index]);
                evalStack.Push(item);
                return true;
            }
            return false;

        }

        public Variable GetVariable(IVariableContainerSession session)
        {
            return new Variable();
        }
    }
}
