using System.Collections.Generic;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo.VM;
using StackItem = Neo.VM.Types.StackItem;

namespace NeoDebug.Neo3
{
    class EvaluationStackContainer : IVariableContainer
    {
        private readonly IReadOnlyList<StackItem> evalStack;

        public EvaluationStackContainer(IReadOnlyList<StackItem> evalStack)
        {
            this.evalStack = evalStack;
        }

        public IEnumerable<Variable> Enumerate(IVariableManager manager)
        {
            for (int i = 0; i < evalStack.Count; i++)
            {
                var v = evalStack[i].ToVariable(manager, $"eval{evalStack.Count - i - 1}");
                yield return v.ForEvaluation("#");
            }
        }
    }
}
