using System;
using System.Collections.Generic;
using StackItem = Neo.VM.Types.StackItem;
using Neo;
using Neo.VM;
using Neo.BlockchainToolkit.TraceDebug;
using System.Linq;

namespace NeoDebug.Neo3
{
    internal partial class TraceApplicationEngine
    {
        private class ExecutionContextAdapter : IExecutionContext
        {
            private readonly TraceRecord.StackFrame frame;

            public ExecutionContextAdapter(TraceRecord.StackFrame frame, IReadOnlyDictionary<UInt160, Script> contracts)
            {
                this.frame = frame;
                Script = contracts[frame.ScriptHash];
            }

            public Instruction CurrentInstruction => Script.GetInstruction(InstructionPointer);

            public int InstructionPointer => frame.InstructionPointer;

            public UInt160 ScriptHash => frame.ScriptHash;

            public Script Script { get; }

            public IReadOnlyList<StackItem> EvaluationStack => frame.EvaluationStack;

            public IReadOnlyList<StackItem> LocalVariables => frame.LocalVariables;

            public IReadOnlyList<StackItem> StaticFields => frame.StaticFields;

            public IReadOnlyList<StackItem> Arguments => frame.Arguments;
        }
    }
}
