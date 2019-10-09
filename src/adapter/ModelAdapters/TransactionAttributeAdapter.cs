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
    internal class TransactionAttributeAdapter : AdapterBase, IVariableProvider, IVariableContainer
    {
        public readonly TransactionAttribute Value;

        public TransactionAttributeAdapter(in TransactionAttribute value)
        {
            Value = value;
        }

        public static TransactionAttributeAdapter Create(in TransactionAttribute value)
        {
            return new TransactionAttributeAdapter(value);
        }

        public bool GetData(ExecutionEngine engine)
        {
            engine.CurrentContext.EvaluationStack.Push(Value.Data.ToArray());
            return true;
        }

        public bool GetUsage(ExecutionEngine engine)
        {
            engine.CurrentContext.EvaluationStack.Push((int)Value.Usage);
            return true;
        }

        public Variable GetVariable(IVariableContainerSession session)
        {
            return new Variable()
            {
                Type = "TransactionAttribute",
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
                Value = Value.Usage.ToString()
            };

            yield return new Variable()
            {
                Name = "Data",
                Value = "<byte array TBD>"
            };
        }
    }
}
