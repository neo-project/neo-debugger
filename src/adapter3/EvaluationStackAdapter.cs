using System;
using System.Collections.Generic;
using Neo.SmartContract;
using StackItem = Neo.VM.Types.StackItem;
using Neo.VM;
using System.Collections;

namespace NeoDebug.Neo3
{
    // will no longer be needed in preview 4: https://github.com/neo-project/neo-vm/pull/359
    internal class EvaluationStackAdapter : IReadOnlyList<StackItem>
    {
        private readonly EvaluationStack evalStack;

        public EvaluationStackAdapter(EvaluationStack evalStack)
        {
            this.evalStack = evalStack;
        }

        public StackItem this[int index] => evalStack.Peek(index);

        public int Count => evalStack.Count;

        public IEnumerator<StackItem> GetEnumerator() => evalStack.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => evalStack.GetEnumerator();
    }
}
