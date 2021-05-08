using System.Collections;
using System.Collections.Generic;

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
                    yield return new ExecutionContextAdapter(s, engine.scriptIdMap);
                }
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }
    }
}
