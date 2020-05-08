using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo.VM;
using NeoDebug.VariableContainers;
using NeoFx;
using NeoFx.Models;



namespace NeoDebug.ModelAdapters
{
    class AccountAdapter : AdapterBase, IVariableProvider
    {
        public readonly Account Item;

        public AccountAdapter(in Account value)
        {
            Item = value;
        }

        public static AccountAdapter Create(in Account value)
        {
            return new AccountAdapter(value);
        }

        public bool GetScriptHash(ExecutionEngine engine)
        {
            if (Item.ScriptHash.TryToArray(out var array))
            {
                engine.CurrentContext.EvaluationStack.Push(array);
                return true;
            }

            return false;
        }

        public bool GetVotes(ExecutionEngine engine)
        {
            var votes = new StackItem[Item.Votes.Length];
            for (int i = 0; i < Item.Votes.Length; i++)
            {
                votes[i] = Item.Votes.Span[i].Key.ToArray();
            }
            engine.CurrentContext.EvaluationStack.Push(votes);
            return true;
        }

        public bool GetBalance(ExecutionEngine engine)
        {
            var evalStack = engine.CurrentContext.EvaluationStack;
            var hash = new UInt256(evalStack.Pop().GetByteArray());
            var balance = Item.Balances.TryGetValue(hash, out var value) ? value : Fixed8.Zero;
            evalStack.Push(balance.AsRawValue());
            return true;
        }

        public Variable GetVariable(IVariableContainerSession session, string name)
        {
            return new Variable()
            {
                Name = name,
                Type = "Account",
                Value = string.Empty
            };
        }
    }
}
