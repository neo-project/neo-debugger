using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo.VM;
using NeoDebug.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NeoDebug.VariableContainers
{
    internal class ExecutionContextContainer : IVariableContainer
    {
        private readonly IVariableContainerSession session;
        private readonly ExecutionContext context;
        private readonly Method method;

        public ExecutionContextContainer(IVariableContainerSession session, ExecutionContext context, Contract contract)
            : this(session, context, contract.GetMethod(context))
        {
        }

        public ExecutionContextContainer(IVariableContainerSession session, ExecutionContext context, Method method)
        {
            this.session = session;
            this.context = context;
            this.method = method;
        }

        public IEnumerable<Variable> GetVariables(VariablesArguments args)
        {
            var debugVariables = method == null
                ? new Parameter[0]
                : method.Parameters.Concat(method.Variables).ToArray();

            if (context.AltStack.Count > 0)
            {
                var variables = (Neo.VM.Types.Array)context.AltStack.Peek(0);

                for (int i = 0; i < variables.Count; i++)
                {
                    var parameter = i < debugVariables.Length
                        ? debugVariables[i]
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
}
