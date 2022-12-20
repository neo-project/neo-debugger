using System;
using System.Collections.Generic;
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
            DebugInfo.Method? method = debugInfo.TryGetMethod(context.InstructionPointer, out var _method)
                ? _method : null;

            var args = EnumerateSlot(manager, DebugSession.ARG_SLOTS_PREFIX, context.Arguments, method?.Parameters);
            var locals = EnumerateSlot(manager, DebugSession.LOCAL_SLOTS_PREFIX, context.LocalVariables, method?.Variables);
            var statics = EnumerateSlot(manager, DebugSession.STATIC_SLOTS_PREFIX, context.StaticFields, debugInfo?.StaticVariables);

            return args.Concat(locals).Concat(statics);

            static IEnumerable<Variable> EnumerateSlot(IVariableManager manager, string prefix, IReadOnlyList<StackItem>? slot, IReadOnlyList<DebugInfo.SlotVariable>? variableList = null)
            {
                variableList ??= Array.Empty<DebugInfo.SlotVariable>();
                slot ??= Array.Empty<StackItem>();

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
