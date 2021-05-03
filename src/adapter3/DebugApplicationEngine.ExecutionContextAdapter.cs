using System;
using System.Collections.Generic;
using Neo.SmartContract;
using StackItem = Neo.VM.Types.StackItem;
using Neo;
using Neo.VM;

namespace NeoDebug.Neo3
{
    internal partial class DebugApplicationEngine
    {
        private class ExecutionContextAdapter : IExecutionContext
        {
            private readonly ExecutionContext context;
            private readonly EvaluationStackAdapter evalStackAdapter;

            public ExecutionContextAdapter(ExecutionContext context, IDictionary<UInt160, UInt160> scriptIdMap)
            {
                this.context = context;
                this.ScriptIdentifier = context.GetScriptHash();

                if (scriptIdMap.TryGetValue(this.ScriptIdentifier, out var scriptHash))
                {
                    this.ScriptHash = scriptHash;
                }
                else
                {
                    this.ScriptHash = Neo.SmartContract.Helper.ToScriptHash(context.Script);
                    scriptIdMap[this.ScriptIdentifier] = this.ScriptHash;
                }

                evalStackAdapter = new EvaluationStackAdapter(context.EvaluationStack);
            }

            public Instruction CurrentInstruction => context.CurrentInstruction;

            public int InstructionPointer => context.InstructionPointer;

            public IReadOnlyList<StackItem> EvaluationStack => evalStackAdapter;

            public IReadOnlyList<StackItem> LocalVariables => Coalese(context.LocalVariables);

            public IReadOnlyList<StackItem> StaticFields => Coalese(context.StaticFields);

            public IReadOnlyList<StackItem> Arguments => Coalese(context.Arguments);

            public Script Script => context.Script;

            public UInt160 ScriptIdentifier { get; }
            public UInt160 ScriptHash { get; }

            static IReadOnlyList<StackItem> Coalese(Neo.VM.Slot? slot) => (slot == null) ? Array.Empty<StackItem>() : slot;
        }

    }
}
