using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo.VM.Types;

namespace NeoDebug.Neo3
{
    internal class TraceStorageContainer : IStorageContainer
    {
        public IEnumerable<Variable> Enumerate(IVariableManager manager)
        {
            return System.Linq.Enumerable.Empty<Variable>();
        }

        public StackItem? Evaluate(ReadOnlyMemory<char> expression)
        {
            return null;
        }
    }
}
