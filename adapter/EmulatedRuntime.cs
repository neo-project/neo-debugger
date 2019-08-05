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
        public void RegisterServices(Action<string, Func<ExecutionEngine, bool>> register)
        {
            register(".Runtime.CheckWitness", CheckWitness);
        }

        private bool CheckWitness(ExecutionEngine engine)
        {
            var evalStack = engine.CurrentContext.EvaluationStack;
            _ = evalStack.Pop().GetByteArray();
            evalStack.Push(true);

            return true;
        }
    }
}
