using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo.VM;
using System;
using System.Collections.Generic;

namespace NeoDebug.VariableContainers
{
    public class ExecutionStackContainer : IVariableContainer
    {
        private readonly IVariableContainerSession session;
        private readonly RandomAccessStack<StackItem> execStack;
        private readonly string stackName;

        public ExecutionStackContainer(IVariableContainerSession session, RandomAccessStack<StackItem> execStack, string stackName)
        {
            this.session = session;
            this.execStack = execStack;
            this.stackName = stackName;
        }

        public IEnumerable<Variable> GetVariables()
        {
            for (var i = 0; i < execStack.Count; i++)
            {
                var item = execStack.Peek(i);
                var variable = item.GetVariable(session, i.ToString());
                variable.EvaluateName = $"${stackName}[{i}]";
                yield return variable;
            }
        }
    }
}
