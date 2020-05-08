using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo.VM;
using NeoDebug.VariableContainers;
using NeoFx.Models;
using System.Collections.Generic;



namespace NeoDebug.ModelAdapters
{
    class TransactionAttributeAdapter : AdapterBase, IVariableProvider, IVariableContainer
    {
        public readonly TransactionAttribute Item;

        public TransactionAttributeAdapter(in TransactionAttribute value)
        {
            Item = value;
        }

        public static TransactionAttributeAdapter Create(in TransactionAttribute value)
        {
            return new TransactionAttributeAdapter(value);
        }

        public bool GetData(ExecutionEngine engine)
        {
            engine.CurrentContext.EvaluationStack.Push(Item.Data.ToArray());
            return true;
        }

        public bool GetUsage(ExecutionEngine engine)
        {
            engine.CurrentContext.EvaluationStack.Push((int)Item.Usage);
            return true;
        }

        public Variable GetVariable(IVariableContainerSession session, string name)
        {
            return new Variable()
            {
                Name = name,
                Type = "TransactionAttribute",
                Value = string.Empty,
                VariablesReference = session.AddVariableContainer(new AdapterVariableContainer(this)),
                NamedVariables = 2,
            };
        }

        public IEnumerable<Variable> GetVariables()
        {
            yield return new Variable()
            {
                Name = "Usage",
                Type = "UsageType",
                Value = Item.Usage.ToString()
            };

            yield return new Variable()
            {
                Name = "Data",
                Value = "<byte array TBD>"
            };
        }
    }
}
