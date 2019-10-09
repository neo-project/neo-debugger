using Neo.VM;
using Neo.VM.Types;
using NeoFx;
using NeoFx.Models;
using NeoFx.Storage;
using System;
using System.Collections.Generic;
using System.Text;

namespace NeoDebug.Adapter
{
    internal partial class InteropService
    {
        public void RegisterBlock(Action<string, Func<ExecutionEngine, bool>, int> register)
        {
            register("Neo.Block.GetTransactionCount", Block_GetTransactionCount, 1);
            register("Neo.Block.GetTransactions", Block_GetTransactions, 1);
            register("Neo.Block.GetTransaction", Block_GetTransaction, 1);

            register("System.Block.GetTransactionCount", Block_GetTransactionCount, 1);
            register("System.Block.GetTransactions", Block_GetTransactions, 1);
            register("System.Block.GetTransaction", Block_GetTransaction, 1);

            register("AntShares.Block.GetTransactionCount", Block_GetTransactionCount, 1);
            register("AntShares.Block.GetTransactions", Block_GetTransactions, 1);
            register("AntShares.Block.GetTransaction", Block_GetTransaction, 1);
        }

        private bool Block_GetTransactionCount(ExecutionEngine engine)
        {
            var evalStack = engine.CurrentContext.EvaluationStack;
            if (evalStack.TryPopContainedStruct<Block>(out var block))
            {
                evalStack.Push(block.Transactions.Length);
                return true;
            }

            return false;
        }

        private bool Block_GetTransactions(ExecutionEngine engine)
        {
            var evalStack = engine.CurrentContext.EvaluationStack;
            if (evalStack.TryPopContainedStruct<Block>(out var block)
                && block.Transactions.Length <= engine.MaxArraySize)
            {
                evalStack.Push(block.Transactions.WrapStackItems());
                return true;
            }

            return false;
        }

        private bool Block_GetTransaction(ExecutionEngine engine)
        {
            var evalStack = engine.CurrentContext.EvaluationStack;
            if (evalStack.Pop() is InteropInterface @interface)
            {
                var container = @interface.GetInterface<StructContainer<Block>>();
                var index = (int)evalStack.Pop().GetBigInteger();
                if (container != null
                    && index >= 0
                    && index < container.Item.Transactions.Length)
                {
                    var tx = container.Item.Transactions.Span[index];
                    evalStack.Push(StructContainer.ToStackItem(tx));
                }
            }

            return false;
        }
    }
}
