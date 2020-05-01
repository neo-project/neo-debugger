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

        public ExecutionStackContainer(IVariableContainerSession session, RandomAccessStack<StackItem> execStack)
        {
            this.session = session;
            this.execStack = execStack;
        }

        public IEnumerable<Variable> GetVariables()
        {
            for (var i = 0; i < execStack.Count; i++)
            {
                var item = execStack.Peek(i);
                yield return item.GetVariable(session, i.ToString());
            }
        }
    }
}
