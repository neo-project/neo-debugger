using System;
using System.Collections.Generic;
using System.Text;
using Neo.VM;
using Neo.VM.Types;
using NeoFx.Models;

namespace NeoDebug.Adapter
{
    internal partial class InteropService
    {
        public void RegisterTransaction(Action<string, Func<ExecutionEngine, bool>, int> register)
        {
            register("Neo.Transaction.GetHash", Transaction_GetHash, 1);
            register("Neo.Transaction.GetType", Transaction_GetType, 1);
            register("Neo.Transaction.GetAttributes", Transaction_GetAttributes, 1);
            register("Neo.Transaction.GetInputs", Transaction_GetInputs, 1);
            register("Neo.Transaction.GetOutputs", Transaction_GetOutputs, 1);
            register("Neo.Transaction.GetReferences", Transaction_GetReferences, 200);
            register("Neo.Transaction.GetUnspentCoins", Transaction_GetUnspentCoins, 200);
            register("Neo.Transaction.GetWitnesses", Transaction_GetWitnesses, 200);

            register("System.Transaction.GetHash", Transaction_GetHash, 1);

            register("AntShares.Transaction.GetHash", Transaction_GetHash, 1);
            register("AntShares.Transaction.GetType", Transaction_GetType, 1);
            register("AntShares.Transaction.GetAttributes", Transaction_GetAttributes, 1);
            register("AntShares.Transaction.GetInputs", Transaction_GetInputs, 1);
            register("AntShares.Transaction.GetOutputs", Transaction_GetOutputs, 1);
            register("AntShares.Transaction.GetReferences", Transaction_GetReferences, 200);
        }

        private bool Transaction_GetWitnesses(ExecutionEngine engine)
        {
            throw new NotImplementedException();
        }

        private bool Transaction_GetUnspentCoins(ExecutionEngine engine)
        {
            throw new NotImplementedException();
        }

        private bool Transaction_GetReferences(ExecutionEngine engine)
        {
            var evalStack = engine.CurrentContext.EvaluationStack;
            if (evalStack.TryPopContainedStruct<Transaction>(out var tx)
                && tx.Inputs.Length <= engine.MaxArraySize)
            {
                var references = new StackItem[tx.Inputs.Length];
                for (int i = 0; i < tx.Inputs.Length; i++)
                {
                    var input = tx.Inputs.Span[i];
                    if (!blockchain.TryGetTransaction(input.PrevHash, out var _, out var refTx)
                        || input.PrevIndex >= refTx.Outputs.Length)
                    {
                        return false;
                    }

                    var output = refTx.Outputs.Span[input.PrevIndex];
                    references[i] = StackItem.FromInterface(new StructContainer<TransactionOutput>(output));
                }

                evalStack.Push(references);
                return true;
            }

            return false;
        }

        private bool Transaction_GetOutputs(ExecutionEngine engine)
        {
            var evalStack = engine.CurrentContext.EvaluationStack;
            if (evalStack.TryPopContainedStruct<Transaction>(out var tx)
                && tx.Outputs.Length <= engine.MaxArraySize)
            {
                evalStack.Push(tx.Outputs.WrapStackItems());
                return true;
            }

            return false;

        }

        private bool Transaction_GetInputs(ExecutionEngine engine)
        {
            var evalStack = engine.CurrentContext.EvaluationStack;
            if (evalStack.TryPopContainedStruct<Transaction>(out var tx)
                && tx.Inputs.Length <= engine.MaxArraySize)
            {
                evalStack.Push(tx.Inputs.WrapStackItems());
                return true;
            }

            return false;
        }

        private bool Transaction_GetAttributes(ExecutionEngine engine)
        {
            var evalStack = engine.CurrentContext.EvaluationStack;
            if (evalStack.TryPopContainedStruct<Transaction>(out var tx)
                && tx.Attributes.Length <= engine.MaxArraySize)
            {
                evalStack.Push(tx.Attributes.WrapStackItems());
                return true;
            }

            return false;
        }

        private bool Transaction_GetType(ExecutionEngine engine)
        {
            var evalStack = engine.CurrentContext.EvaluationStack;

            if (evalStack.TryPopContainedStruct<Transaction>(out var tx))
            {
                evalStack.Push((int)tx.Type);
                return true;
            }

            return false;
        }

        private bool Transaction_GetHash(ExecutionEngine engine)
        {
            var evalStack = engine.CurrentContext.EvaluationStack;

            if (evalStack.TryPopContainedStruct<Transaction>(out var tx)
                && NeoFx.Utility.TryHash(tx, out var hash)
                && hash.TryToArray(out var array))
            {
                evalStack.Push(array);
                return true;
            }

            return false;
        }
    }
}
