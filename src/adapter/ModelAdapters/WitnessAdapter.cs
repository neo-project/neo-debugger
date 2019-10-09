using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo.VM;
using NeoDebug.VariableContainers;
using NeoFx.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace NeoDebug.Adapter.ModelAdapters
{
    class WitnessAdapter : AdapterBase, IVariableProvider
    {
        public readonly Witness Value;

        public WitnessAdapter(in Witness value)
        {
            Value = value;
        }

        public static WitnessAdapter Create(in Witness value)
        {
            return new WitnessAdapter(value);
        }

        public bool GetVerificationScript(ExecutionEngine engine)
        {
            engine.CurrentContext.EvaluationStack.Push(Value.VerificationScript.ToArray());
            return true;
        }

        public Variable GetVariable(IVariableContainerSession session)
        {
            return new Variable()
            {
                Type = "Witness"
            };
        }
    }
}
