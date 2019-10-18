using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo.VM;
using NeoDebug.Models;
using System.Collections.Generic;
using System.Linq;

namespace NeoDebug.VariableContainers
{
    internal class ExecutionContextContainer : IVariableContainer
    {
        private readonly IVariableContainerSession session;
        private readonly ExecutionContext context;
        private readonly Method? method;

        public ExecutionContextContainer(IVariableContainerSession session, ExecutionContext context, Contract contract)
            : this(session, context, contract.GetMethod(context))
        {
        }

        public ExecutionContextContainer(IVariableContainerSession session, ExecutionContext context, Method? method)
        {
            this.session = session;
            this.context = context;
            this.method = method;
        }

        public IEnumerable<Variable> GetVariables()
        {
            if (context.AltStack.Count > 0)
            {
                var variables = (Neo.VM.Types.Array)context.AltStack.Peek(0);

                for (int i = 0; i < variables.Count; i++)
                {
                    var parameter = method?.Locals.ElementAtOrDefault(i);
                    var variable = variables[i].GetVariable(session,
                        parameter?.Name ?? "<variable {i}>",
                        parameter?.Type);
                    yield return variable;
                }
            }
        }
    }
}
