using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo.VM;
using NeoDebug.VariableContainers;
using System.Collections.Generic;

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
        EvaluateResponse EvaluateStorageExpression(IVariableContainerSession session, EvaluateArguments args);
    }
}
