using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo.VM;
using NeoDebug.VariableContainers;
using NeoFx.Models;
using NeoFx.Storage;
using System;
using System.Collections.Generic;
using System.Diagnostics;



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

        public bool GetReferences(ExecutionEngine engine, IBlockchainStorage? blockchain)
        {
            if (blockchain != null
                && Item.Inputs.Length <= engine.MaxArraySize)
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
            if (NeoFx.Helpers.TryHash(Item, out var hash)
                && hash.TryToArray(out var array))
            {
                engine.CurrentContext.EvaluationStack.Push(array);
                return true;
            }

            return false;
        }

        public bool GetScript(ExecutionEngine engine)
        {
            if (BinaryFormat.TryReadInvocationData(Item, out var script, out var _))
            {
                engine.CurrentContext.EvaluationStack.Push(script.ToArray());
                return true;
            }

            return false;
        }

        public bool GetUnspentCoins(ExecutionEngine engine, IBlockchainStorage? blockchain)
        {
            if (NeoFx.Helpers.TryHash(Item, out var hash)
                && blockchain != null
                && blockchain.TryGetUnspentCoins(hash, out var coinStates))
            {
                Debug.Assert(Item.Outputs.Length == coinStates.Length);

                // In order to avoid memory allocations, we traverse the list of coin states twice.
                // The first time is to count the number of unspent coins. 
                int unspentCount = 0;
                for (int i = 0; i < coinStates.Length; i++)
                {
                    if ((coinStates.Span[i] & CoinState.Spent) == 0)
                    {
                        unspentCount++;
                    }
                }

                // Once we know how many unspent coins there are, we can allocate a stack item array
                // of the correct length and traverse the list of coin states again to populate it
                var unspentCoins = new StackItem[unspentCount];
                var currentOutput = 0;
                for (int i = 0; i < coinStates.Length; i++)
                {
                    Debug.Assert(currentOutput < coinStates.Length);

                    if ((coinStates.Span[i] & CoinState.Spent) == 0)
                    {
                        unspentCoins[currentOutput++] = new TransactionOutputAdatper(Item.Outputs.Span[i]);
                    }
                }

                engine.CurrentContext.EvaluationStack.Push(unspentCoins);
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
                Value = Item.Type.ToString(),
            };

            if (NeoFx.Helpers.TryHash(Item, out var hash))
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
            var buffer = new byte[Item.GetSize()];
            if (NeoFx.Helpers.TryWriteHashData(Item, buffer, out var bytesWritten))
            {
                Debug.Assert(bytesWritten == buffer.Length);
                return buffer;
            }

            throw new Exception("TryWriteHashData failed");
        }
    }
}
