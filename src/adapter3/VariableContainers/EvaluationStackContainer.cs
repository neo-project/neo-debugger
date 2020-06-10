using System.Collections.Generic;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo.VM;

namespace NeoDebug.Neo3
{
    class EvaluationStackContainer : IVariableContainer
    {
        private readonly IVariableManager manager;
        private readonly EvaluationStack evalStack;

        public EvaluationStackContainer(IVariableManager manager, EvaluationStack evalStack)
        {
            this.manager = manager;
            this.evalStack = evalStack;
        }

        public IEnumerable<Variable> Enumerate()
        {
            for (int i = 0; i < evalStack.Count; i++)
            {
                var v = evalStack.Peek(i).ToVariable(manager, $"evalStack-{evalStack.Count - i - 1}");
                v.EvaluateName = v.Name;
                yield return v;
            }
        }
    }
}
