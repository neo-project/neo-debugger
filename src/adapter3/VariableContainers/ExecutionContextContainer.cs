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
        readonly IExecutionContext context;
        readonly DebugInfo? debugInfo;
        readonly byte addressVersion;

        public ExecutionContextContainer(IExecutionContext context, DebugInfo? debugInfo, byte addressVersion)
        {
            this.context = context;
            this.debugInfo = debugInfo;
            this.addressVersion = addressVersion;
        }

        public IEnumerable<Variable> Enumerate(IVariableManager manager)
        {
            var method = debugInfo?.TryGetMethod(context.InstructionPointer, out var _method) ?? false
                ? _method : default;

            var args = EnumerateSlot(DebugSession.ARG_SLOTS_PREFIX, context.Arguments, method.Parameters);
            var locals = EnumerateSlot(DebugSession.LOCAL_SLOTS_PREFIX, context.LocalVariables, method.Variables);
            var statics = EnumerateSlot(DebugSession.STATIC_SLOTS_PREFIX, context.StaticFields, debugInfo?.StaticVariables);

            return args.Concat(locals).Concat(statics);

            IEnumerable<Variable> EnumerateSlot(string prefix, IReadOnlyList<StackItem>? slotItems, IReadOnlyList<DebugInfo.SlotVariable>? slotVariables = null)
            {
                slotVariables ??= ImmutableList<DebugInfo.SlotVariable>.Empty;
                slotItems ??= ImmutableList<StackItem>.Empty;

                foreach (var slotVar in slotVariables)
                {
                    var slotItem = slotVar.Index < slotItems.Count
                        ? slotItems[slotVar.Index]
                        : StackItem.Null;
                    var variable = slotItem.AsVariable(manager, slotVar.Name, slotVar.Type, addressVersion);
                    variable.EvaluateName = slotVar.Name;
                    yield return variable;
                }
            }
        }
    }
}
