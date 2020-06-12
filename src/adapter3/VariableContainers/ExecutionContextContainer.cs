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

            var args = EnumerateSlot("arg", context.Arguments, method?.Parameters);
            var locals = EnumerateSlot("local", context.LocalVariables, method?.Variables);
            // TODO: statics

            return args.Concat(locals);

            IEnumerable<Variable> EnumerateSlot(string prefix, IReadOnlyList<StackItem>? slot, IList<(string name, string type)>? variableInfo = null)
            {
                variableInfo ??= new List<(string name, string type)>();
                slot ??= new List<StackItem>();
                for (int i = 0; i < variableInfo.Count; i++)
                {
                    var (name, type) = variableInfo[i];
                    if (name.Contains(':')) continue;
                    var v = i < slot.Count 
                        ? slot[i].ToVariable(manager, name, type) 
                        : StackItem.Null.ToVariable(manager, name, type);

                    yield return v;
                }
            }
        }
    }
}
