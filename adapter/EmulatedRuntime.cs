using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo.VM;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Neo.DebugAdapter
{
    internal class EmulatedRuntime
    {
        bool? checkWitnessBypass;

        public void RegisterServices(Action<string, Func<ExecutionEngine, bool>> register)
        {
            register(".Runtime.CheckWitness", CheckWitness);
        }

        public void BypassCheckWitness(bool value)
        {
            checkWitnessBypass = value;
        }

        private bool CheckWitness(ExecutionEngine engine)
        {
            if (checkWitnessBypass.HasValue)
            {
                var evalStack = engine.CurrentContext.EvaluationStack;
                _ = evalStack.Pop().GetByteArray();
                evalStack.Push(checkWitnessBypass.Value);
                return true;
            }

            return false;
        }
    }
}
