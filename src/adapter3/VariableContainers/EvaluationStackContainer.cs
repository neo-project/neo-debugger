using System.Collections.Generic;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo.VM;

namespace NeoDebug.Neo3
{
    class EvaluationStackContainer : IVariableContainer
    {
        private readonly EvaluationStack evalStack;

        public EvaluationStackContainer(EvaluationStack evalStack)
        {
            this.evalStack = evalStack;
        }

        public IEnumerable<Variable> Enumerate(IVariableManager manager)
        {
            for (int i = 0; i < evalStack.Count; i++)
            {
                var v = evalStack.Peek(i).ToVariable(manager, $"eval{evalStack.Count - i - 1}");
                yield return v;
            }
        }
    }
}
