using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo.VM;
using NeoDebug.VariableContainers;
using NeoFx.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace NeoDebug.Adapter.ModelAdapters
{
    class DeployedContractAdapter : AdapterBase, IVariableProvider
    {
        public readonly DeployedContract Item;

        public DeployedContractAdapter(in DeployedContract value)
        {
            Item = value;
        }

        public static DeployedContractAdapter Create(in DeployedContract value)
        {
            return new DeployedContractAdapter(value);
        }

        public bool GetScript(ExecutionEngine engine)
        {
            engine.CurrentContext.EvaluationStack.Push(Item.Script.ToArray());
            return true;
        }

        public bool IsPayable(ExecutionEngine engine)
        {
            engine.CurrentContext.EvaluationStack.Push(Item.Payable);
            return true;
        }

        public Variable GetVariable(IVariableContainerSession session)
        {
            return new Variable();
        }
    }
}
