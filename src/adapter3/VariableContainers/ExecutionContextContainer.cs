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
            var method = debugInfo?.GetMethod(context.InstructionPointer);

            return EnumerateSlot("arg", context.Arguments, method?.Parameters)
                .Concat(EnumerateSlot("local", context.LocalVariables, method?.Variables))
                .Concat(EnumerateSlot("static", context.StaticFields));

            IEnumerable<Variable> EnumerateSlot(string prefix, IReadOnlyList<StackItem>? slot, IList<(string name, string type)>? variableInfo = null)
            {
                variableInfo ??= new List<(string name, string type)>();
                slot ??= new List<StackItem>();
                for (int i = 0; i < slot.Count; i++)
                {
                    var (name, type) = i < variableInfo.Count ? variableInfo[i] : ($"{prefix}{i}" , string.Empty);
                    var v = slot[i].ToVariable(manager, name, type);
                    v.EvaluateName = v.Name;
                    yield return v;
                }
            }
        }
    }
}
