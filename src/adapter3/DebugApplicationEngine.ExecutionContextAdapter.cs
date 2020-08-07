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

            public ExecutionContextAdapter(ExecutionContext context)
            {
                this.context = context;
                evalStackAdapter = new EvaluationStackAdapter(context.EvaluationStack);
            }

            public Instruction CurrentInstruction => context.CurrentInstruction;

            public int InstructionPointer => context.InstructionPointer;

            public IReadOnlyList<StackItem> EvaluationStack => evalStackAdapter;

            public IReadOnlyList<StackItem> LocalVariables => context.LocalVariables;

            public IReadOnlyList<StackItem> StaticFields => context.StaticFields;

            public IReadOnlyList<StackItem> Arguments => context.Arguments;

            public Script Script => context.Script;

            public UInt160 GetScriptHash() => context.GetScriptHash();
        }

    }
}
