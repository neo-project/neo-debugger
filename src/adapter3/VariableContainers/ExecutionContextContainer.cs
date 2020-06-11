using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo.VM;
using Neo.VM.Types;

namespace NeoDebug.Neo3
{
    class ExecutionContextContainer : IVariableContainer
    {
        private readonly ExecutionContext context;
        private readonly DebugInfo? debugInfo;

        public ExecutionContextContainer(ExecutionContext context, DebugInfo? debugInfo)
        {
            this.context = context;
            this.debugInfo = debugInfo;
        }

        public IEnumerable<Variable> Enumerate(IVariableManager manager)
        {
            return EnumerateSlot("arg", context.Arguments)
                .Concat(EnumerateSlot("local", context.LocalVariables))
                .Concat(EnumerateSlot("static", context.StaticFields));

            IEnumerable<Variable> EnumerateSlot(string prefix, IReadOnlyList<StackItem>? slot)
            {
                slot ??= new List<StackItem>();
                for (int i = 0; i < slot.Count; i++)
                {
                    var v = slot[i].ToVariable(manager, $"{prefix}{i}");
                    v.EvaluateName = v.Name;
                    yield return v;
                }
            }
        }
    }
}
