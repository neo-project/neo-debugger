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
        public readonly Transaction Item;

        public TransactionAdapter(in Transaction value)
        {
            Item = value;
        }

        public static TransactionAdapter Create(in Transaction value)
        {
            return new TransactionAdapter(value);
        }

        public bool GetReferences(ExecutionEngine engine, IBlockchainStorage blockchain)
        {
            if (Item.Inputs.Length <= engine.MaxArraySize)
            {
                var references = new StackItem[Item.Inputs.Length];
                for (int i = 0; i < Item.Inputs.Length; i++)
                {
                    var input = Item.Inputs.Span[i];
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
            if (Item.Outputs.Length <= engine.MaxArraySize)
            {
                var items = Item.Outputs.WrapStackItems(TransactionOutputAdatper.Create);
                engine.CurrentContext.EvaluationStack.Push(items);
                return true;
            }
            return false;
        }

        public bool GetInputs(ExecutionEngine engine)
        {
            if (Item.Inputs.Length <= engine.MaxArraySize)
            {
                var items = Item.Inputs.WrapStackItems(CoinReferenceAdapter.Create);
                engine.CurrentContext.EvaluationStack.Push(items);
                return true;
            }
            return false;
        }

        public bool GetAttributes(ExecutionEngine engine)
        {
            if (Item.Attributes.Length <= engine.MaxArraySize)
            {
                var items = Item.Attributes.WrapStackItems(TransactionAttributeAdapter.Create);
                engine.CurrentContext.EvaluationStack.Push(items);
                return true;
            }
            return false;
        }

        public bool GetWitnesses(ExecutionEngine engine)
        {
            if (Item.Witnesses.Length <= engine.MaxArraySize)
            {
                var items = Item.Witnesses.WrapStackItems(WitnessAdapter.Create);
                engine.CurrentContext.EvaluationStack.Push(items);
                return true;
            }
            return false;
        }

        public bool GetType(ExecutionEngine engine)
        {
            engine.CurrentContext.EvaluationStack.Push((int)Item.Type);
            return true;
        }

        public bool GetHash(ExecutionEngine engine)
        {
            if (NeoFx.Utility.TryHash(Item, out var hash)
                && hash.TryToArray(out var array))
            {
                engine.CurrentContext.EvaluationStack.Push(array);
                return true;
            }

            return false;
        }

        public bool GetScript(ExecutionEngine engine)
        {
            throw new NotImplementedException();
        }

        public bool GetUnspentCoins(ExecutionEngine engine)
        {
            throw new NotImplementedException();
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
                Value = Item.Type.ToString(),
            };

            if (NeoFx.Utility.TryHash(Item, out var hash))
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
