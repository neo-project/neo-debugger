using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo.VM;
using NeoDebug.VariableContainers;
using NeoFx.Models;
using NeoFx.Storage;
using System;
using System.Collections.Generic;
using System.Text;

namespace NeoDebug.Adapter.ModelAdapters
{
    internal class TransactionAdapter : AdapterBase, IScriptContainer, IVariableProvider, IVariableContainer
    {
        public readonly Transaction Value;

        public TransactionAdapter(in Transaction value)
        {
            Value = value;
        }

        public bool GetReferences(ExecutionEngine engine, IBlockchainStorage blockchain)
        {
            if (Value.Inputs.Length <= engine.MaxArraySize)
            {
                var references = new StackItem[Value.Inputs.Length];
                for (int i = 0; i < Value.Inputs.Length; i++)
                {
                    var input = Value.Inputs.Span[i];
                    if (!blockchain.TryGetTransaction(input.PrevHash, out var _, out var refTx)
                        || input.PrevIndex >= refTx.Outputs.Length)
                    {
                        return false;
                    }

                    var output = refTx.Outputs.Span[input.PrevIndex];
                    references[i] = new TransactionOutputAdatper(output);
                }

                engine.CurrentContext.EvaluationStack.Push(references);
                return true;
            }

            return false;
        }

        public bool GetOutputs(ExecutionEngine engine)
        {
            if (Value.Outputs.Length <= engine.MaxArraySize)
            {
                var items = Value.Outputs.WrapStackItems(TransactionOutputAdatper.Create);
                engine.CurrentContext.EvaluationStack.Push(items);
                return true;
            }

            return false;
        }

        public bool GetInputs(ExecutionEngine engine)
        {
            if (Value.Inputs.Length <= engine.MaxArraySize)
            {
                var items = Value.Inputs.WrapStackItems(CoinReferenceAdapter.Create);
                engine.CurrentContext.EvaluationStack.Push(items);
                return true;
            }

            return false;
        }

        public bool GetAttributes(ExecutionEngine engine)
        {
            if (Value.Attributes.Length <= engine.MaxArraySize)
            {
                var items = Value.Attributes.WrapStackItems(TransactionAttributeAdapter.Create);
                engine.CurrentContext.EvaluationStack.Push(items);
                return true;
            }

            return false;
        }

        public bool GetType(ExecutionEngine engine)
        {
            engine.CurrentContext.EvaluationStack.Push((int)Value.Type);
            return true;
        }

        public bool GetHash(ExecutionEngine engine)
        {
            if (NeoFx.Utility.TryHash(Value, out var hash)
                && hash.TryToArray(out var array))
            {
                engine.CurrentContext.EvaluationStack.Push(array);
                return true;
            }

            return false;
        }

        public Variable GetVariable(IVariableContainerSession session)
        {
            return new Variable()
            {
                Type = "Transaction",
                VariablesReference = session.AddVariableContainer(new AdapterVariableContainer(this)),
                NamedVariables = 2,
            };
        }

        public IEnumerable<Variable> GetVariables()
        {
            yield return new Variable()
            {
                Name = "Type",
                Value = Value.Type.ToString(),
            };

            if (NeoFx.Utility.TryHash(Value, out var hash))
            {
                yield return new Variable()
                {
                    Name = "Hash",
                    Value = hash.ToString(),
                };
            }
        }

        byte[] IScriptContainer.GetMessage()
        {
            throw new NotImplementedException();
        }
    }
}
