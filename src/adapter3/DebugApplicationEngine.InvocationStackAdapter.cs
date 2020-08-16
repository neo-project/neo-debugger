using System;
using System.Collections;
using System.Collections.Generic;
using Neo.SmartContract;
using StackItem = Neo.VM.Types.StackItem;
using System.Linq;

namespace NeoDebug.Neo3
{
    internal partial class DebugApplicationEngine
    {
        private class InvocationStackAdapter : IReadOnlyCollection<IExecutionContext>
        {
            private readonly DebugApplicationEngine engine;

            public InvocationStackAdapter(DebugApplicationEngine engine)
            {
                this.engine = engine;
            }

            public int Count => engine.InvocationStack.Count;

            public IEnumerator<IExecutionContext> GetEnumerator()
            {
                foreach (var s in engine.InvocationStack)
                {
                    yield return new ExecutionContextAdapter(s);
                }
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }
    }
}
