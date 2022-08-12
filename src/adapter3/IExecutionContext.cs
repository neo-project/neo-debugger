using System.Collections.Generic;
using Neo.SmartContract;
using StackItem = Neo.VM.Types.StackItem;
using Neo;
using Neo.VM;

namespace NeoDebug.Neo3
{
    internal interface IExecutionContext
    {
        Instruction? CurrentInstruction { get; }
        int InstructionPointer { get; }
        UInt160 ScriptHash { get; }
        Script Script { get; }
        IReadOnlyList<MethodToken> Tokens { get; }
        uint? NefChecksum { get; }

        IReadOnlyList<StackItem> EvaluationStack { get; }
        IReadOnlyList<StackItem> LocalVariables { get; }
        IReadOnlyList<StackItem> StaticFields { get; }
        IReadOnlyList<StackItem> Arguments { get; }
    }
}
