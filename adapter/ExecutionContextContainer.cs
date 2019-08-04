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
        private readonly NeoDebugSession session;
        private readonly ExecutionContext context;
        private readonly Method method;

        public ExecutionContextContainer(NeoDebugSession session, ExecutionContext context)
        {
            this.session = session;
            this.context = context;
            method = session.Contract.GetMethod(context);
        }

        public IEnumerable<Variable> GetVariables(VariablesArguments args)
        {
            var variables = (Neo.VM.Types.Array)context.AltStack.Peek(0);

            for (int i = 0; i < variables.Count; i++)
            {
                var parameter = method != null && i < method.Parameters.Count
                    ? method.Parameters[i]
                    : null;
                var variable = variables[i].GetVariable(session, parameter);
                variable.Name = string.IsNullOrEmpty(variable.Name)
                    ? $"<variable {i}>"
                    : variable.Name;

                yield return variable;
            }
        }
    }
}
