using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo.BlockchainToolkit.Models;
using Neo.SmartContract;

namespace NeoDebug.Neo3
{
    using StackItem = Neo.VM.Types.StackItem;

    class ExecutionContextContainer : IVariableContainer
    {
        private readonly IExecutionContext context;
        private readonly DebugInfo? debugInfo;

        public ExecutionContextContainer(IExecutionContext context, DebugInfo? debugInfo)
        {
            this.context = context;
            this.debugInfo = debugInfo;
        }

        public IEnumerable<Variable> Enumerate(IVariableManager manager)
        {
            var method = debugInfo?.GetMethod(context.InstructionPointer);

            var args = EnumerateSlot(DebugSession.ARG_SLOTS_PREFIX, context.Arguments, method?.Parameters);
            var locals = EnumerateSlot(DebugSession.LOCAL_SLOTS_PREFIX, context.LocalVariables, method?.Variables);
            var statics = EnumerateSlot(DebugSession.STATIC_SLOTS_PREFIX, context.StaticFields, debugInfo?.StaticVariables);

            return args.Concat(locals).Concat(statics);

            IEnumerable<Variable> EnumerateSlot(string prefix, IReadOnlyList<StackItem>? slot, IReadOnlyList<(string name, string type, uint? slotIndex)>? variableInfo = null)
            {
                variableInfo ??= ImmutableList<(string name, string type, uint? slotIndex)>.Empty;
                slot ??= ImmutableList<StackItem>.Empty;

                var variableCount = System.Math.Max(variableInfo.Count, slot.Count);
                for (int i = 0; i < variableCount; i++)
                {
                    var name = i < variableInfo.Count ? variableInfo[i].name : $"{prefix}{i}";
                    var type = i < variableInfo.Count && Enum.TryParse<ContractParameterType>(variableInfo[i].type, out var _type) 
                        ? _type 
                        : ContractParameterType.Any;
                    var item = i < slot.Count ? slot[i] : StackItem.Null;
                    var variable = item.ToVariable(manager, name, type);
                    variable.EvaluateName = variable.Name;
                    yield return variable;
                }
            }
        }
    }
}
