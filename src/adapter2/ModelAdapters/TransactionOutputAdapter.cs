using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo.VM;
using NeoDebug.VariableContainers;
using NeoFx.Models;
using System.Collections.Generic;




namespace NeoDebug.ModelAdapters
{
    class TransactionOutputAdatper : AdapterBase, IVariableProvider, IVariableContainer
    {
        public readonly TransactionOutput Item;

        public TransactionOutputAdatper(in TransactionOutput value)
        {
            Item = value;
        }

        public static TransactionOutputAdatper Create(in TransactionOutput value)
        {
            return new TransactionOutputAdatper(value);
        }

        public bool GetAssetId(ExecutionEngine engine)
        {
            if (Item.AssetId.TryToArray(out var array))
            {
                engine.CurrentContext.EvaluationStack.Push(array);
                return true;
            }

            return false;
        }

        public bool GetValue(ExecutionEngine engine)
        {
            engine.CurrentContext.EvaluationStack.Push(Item.Value.AsRawValue());
            return true;
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

        public Variable GetVariable(IVariableContainerSession session, string name)
        {
            return new Variable()
            {
                Name = name,
                Type = "TransactionOutput",
                Value = string.Empty,
                VariablesReference = session.AddVariableContainer(new AdapterVariableContainer(this)),
                NamedVariables = 3,
            };
        }

        public IEnumerable<Variable> GetVariables()
        {
            yield return new Variable()
            {
                Name = "AssetId",
                Type = "UInt256",
                Value = Item.AssetId.ToString()
            };

            yield return new Variable()
            {
                Name = "Value",
                Type = "long",
                Value = Item.Value.ToString()
            };

            yield return new Variable()
            {
                Name = "ScriptHash",
                Type = "UInt160",
                Value = Item.ScriptHash.ToString()
            };
        }
    }
}
