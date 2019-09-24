using Neo.VM;
using NeoDebug.Models;
using NeoDebug.VariableContainers;
using System;
using System.Collections.Generic;
using System.Text;

namespace NeoDebug
{
    public interface IExecutionEngine
    {
        VMState State { get; set; }
        IEnumerable<StackItem> ResultStack { get; }
        ExecutionContext CurrentContext { get; }

        void ExecuteNext();
        ExecutionContext LoadScript(byte[] script, int rvcount = -1);
        RandomAccessStack<ExecutionContext> InvocationStack { get; }

        IVariableContainer GetStorageContainer(IVariableContainerSession session);
    }
}
