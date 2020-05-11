using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo.VM;
using NeoDebug.VariableContainers;
using NeoFx.Models;



namespace NeoDebug.ModelAdapters
{
    class BlockAdapter : AdapterBase, IVariableProvider
    {
        public readonly Block Item;

        public BlockAdapter(in Block value)
        {
            Item = value;
        }

        public static BlockAdapter Create(in Block value)
        {
            return new BlockAdapter(value);
        }

        public bool GetTransactionCount(ExecutionEngine engine)
        {
            engine.CurrentContext.EvaluationStack.Push(Item.Transactions.Length);
            return true;
        }

        public bool GetTransactions(ExecutionEngine engine)
        {
            if (Item.Transactions.Length <= engine.MaxArraySize)
            {
                var items = Item.Transactions.WrapStackItems(TransactionAdapter.Create);

                engine.CurrentContext.EvaluationStack.Push(items);
                return true;
            }
            return false;
        }

        public bool GetTransaction(ExecutionEngine engine)
        {
            var evalStack = engine.CurrentContext.EvaluationStack;
            var index = (int)evalStack.Pop().GetBigInteger();
            if (index >= 0 && index < Item.Transactions.Length)
            {
                var item = new TransactionAdapter(Item.Transactions.Span[index]);
                evalStack.Push(item);
                return true;
            }
            return false;

        }

        public Variable GetVariable(IVariableContainerSession session, string name)
        {
            return new Variable()
            {
                Name = name,
                Type = "Block",
                Value = string.Empty
            };
        }
    }
}
