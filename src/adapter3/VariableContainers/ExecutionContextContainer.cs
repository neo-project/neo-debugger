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

            IEnumerable<Variable> EnumerateSlot(string prefix, IReadOnlyList<StackItem>? slot, IReadOnlyList<DebugInfo.SlotVariable>? variableList = null)
            {
                variableList ??= ImmutableList<DebugInfo.SlotVariable>.Empty;
                slot ??= ImmutableList<StackItem>.Empty;

                for (int i = 0; i < variableList.Count; i++)
                {
                    var slotIndex = variableList[i].Index; 
                    var type = Enum.TryParse<ContractParameterType>(variableList[i].Type, out var _type)
                        ? _type
                        : ContractParameterType.Any;
                    var item = slotIndex < slot.Count ? slot[slotIndex] : StackItem.Null;
                    var variable = item.ToVariable(manager, variableList[i].Name, type);
                    variable.EvaluateName = variable.Name;
                    yield return variable;
                }
            }
        }
    }
}
