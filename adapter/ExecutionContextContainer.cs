using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo.VM;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Neo.DebugAdapter
{
    internal class ExecutionContextContainer : IVariableContainer
    {
        private readonly ExecutionContext context;
        private readonly Method method;

        public ExecutionContextContainer(ExecutionContext context, Method method)
        {
            this.context = context;
            this.method = method;
        }

        public IEnumerable<Variable> GetVariables(VariablesArguments args)
        {
            if (method != null && context.AltStack.Peek(0) is Neo.VM.Types.Array alt)
            {
                for (int j = 0; j < method.Parameters.Count; j++)
                {
                    var p = method.Parameters[j];
                    var value = alt[j].GetStackItemValue(p.Type);
                    yield return new Variable(p.Name, value, 0);
                }

                for (int j = method.Parameters.Count; j < alt.Count; j++)
                {
                    var value = alt[j].GetStackItemValue();
                    yield return new Variable($"<variable {j}>", value, 0);
                }
            }
        }
    }
}
