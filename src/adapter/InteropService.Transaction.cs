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
            register("Neo.InvocationTransaction.GetScript", InvocationTransaction_GetScript, 1);
            register("Neo.Input.GetHash", Input_GetHash, 1);
            register("Neo.Input.GetIndex", Input_GetIndex, 1);
            register("Neo.Output.GetAssetId", Output_GetAssetId, 1);
            register("Neo.Output.GetValue", Output_GetValue, 1);
            register("Neo.Output.GetScriptHash", Output_GetScriptHash, 1);
            register("Neo.Witness.GetVerificationScript", Witness_GetVerificationScript, 100);
            register("Neo.Attribute.GetUsage", Attribute_GetUsage, 1);
            register("Neo.Attribute.GetData", Attribute_GetData, 1);

            register("System.Transaction.GetHash", Transaction_GetHash, 1);

            register("AntShares.Transaction.GetHash", Transaction_GetHash, 1);
            register("AntShares.Transaction.GetType", Transaction_GetType, 1);
            register("AntShares.Transaction.GetAttributes", Transaction_GetAttributes, 1);
            register("AntShares.Transaction.GetInputs", Transaction_GetInputs, 1);
            register("AntShares.Transaction.GetOutputs", Transaction_GetOutputs, 1);
            register("AntShares.Transaction.GetReferences", Transaction_GetReferences, 200);
            register("AntShares.Input.GetHash", Input_GetHash, 1);
            register("AntShares.Input.GetIndex", Input_GetIndex, 1);
            register("AntShares.Output.GetAssetId", Output_GetAssetId, 1);
            register("AntShares.Output.GetValue", Output_GetValue, 1);
            register("AntShares.Output.GetScriptHash", Output_GetScriptHash, 1);
            register("AntShares.Attribute.GetUsage", Attribute_GetUsage, 1);
            register("AntShares.Attribute.GetData", Attribute_GetData, 1);
        }

        private bool Attribute_GetData(ExecutionEngine engine)
        {
            var evalStack = engine.CurrentContext.EvaluationStack;
            if (evalStack.TryPopContainedStruct<TransactionAttribute>(out var attrib))
            {
                evalStack.Push(attrib.Data.ToArray());
                return true;
            }

            return false;
        }

        private bool Attribute_GetUsage(ExecutionEngine engine)
        {
            var evalStack = engine.CurrentContext.EvaluationStack;
            if (evalStack.TryPopContainedStruct<TransactionAttribute>(out var attrib))
            {
                evalStack.Push((int)attrib.Usage);
                return true;
            }

            return false;
        }

        private bool Witness_GetVerificationScript(ExecutionEngine engine)
        {
            var evalStack = engine.CurrentContext.EvaluationStack;
            if (evalStack.TryPopContainedStruct<Witness>(out var witness))
            {
                evalStack.Push(witness.VerificationScript.ToArray());
                return true;
            }

            return false;
        }

        private bool InvocationTransaction_GetScript(ExecutionEngine engine)
        {
            throw new NotImplementedException();
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

        private bool Input_GetIndex(ExecutionEngine engine)
        {
            var evalStack = engine.CurrentContext.EvaluationStack;

            if (evalStack.TryPopContainedStruct<CoinReference>(out var input))
            {
                evalStack.Push((int)input.PrevIndex);
                return true;
            }

            return false;
        }

        private bool Input_GetHash(ExecutionEngine engine)
        {
            var evalStack = engine.CurrentContext.EvaluationStack;

            if (evalStack.TryPopContainedStruct<CoinReference>(out var output)
                && output.PrevHash.TryToArray(out var array))
            {
                evalStack.Push(array);
                return true;
            }

            return false;
        }

        private bool Output_GetAssetId(ExecutionEngine engine)
        {
            var evalStack = engine.CurrentContext.EvaluationStack;

            if (evalStack.TryPopContainedStruct<TransactionOutput>(out var output)
                && output.AssetId.TryToArray(out var array))
            {
                evalStack.Push(array);
                return true;
            }

            return false;
        }

        private bool Output_GetValue(ExecutionEngine engine)
        {
            var evalStack = engine.CurrentContext.EvaluationStack;

            if (evalStack.TryPopContainedStruct<TransactionOutput>(out var output))
            {
                evalStack.Push(output.Value);
                return true;
            }

            return false;
        }

        private bool Output_GetScriptHash(ExecutionEngine engine)
        {
            var evalStack = engine.CurrentContext.EvaluationStack;

            if (evalStack.TryPopContainedStruct<TransactionOutput>(out var output)
                && output.ScriptHash.TryToArray(out var array))
            {
                evalStack.Push(array);
                return true;
            }

            return false;
        }
    }
}
