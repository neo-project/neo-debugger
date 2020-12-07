using System;
using System.Collections.Generic;
using StackItem = Neo.VM.Types.StackItem;
using Neo;
using Neo.VM;
using Neo.BlockchainToolkit.TraceDebug;
using Neo.SmartContract.Native;

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
                if (contracts.TryGetValue(frame.ScriptHash, out var script))
                {
                    Script = script;
                }
                else if (TryGetNativeContract(frame.ScriptIdentifier, out script))
                {
                    Script = script;
                }
                else
                {
                    throw new Exception($"Cannot load script {frame.ScriptHash}");
                }

                static bool TryGetNativeContract(UInt160 scriptHash, out Script script)
                {
                    foreach (var nativeContract in NativeContract.Contracts)
                    {
                        if (scriptHash == nativeContract.Hash)
                        {
                            script = nativeContract.Script;
                            return true;
                        }
                    }

                    script = default!;
                    return false;
                }
            }

            public Instruction CurrentInstruction => Script.GetInstruction(InstructionPointer);

            public int InstructionPointer => frame.InstructionPointer;

            public UInt160 ScriptIdentifier => frame.ScriptIdentifier;
            public UInt160 ScriptHash => frame.ScriptHash;

            public Script Script { get; }

            public IReadOnlyList<StackItem> EvaluationStack => frame.EvaluationStack;

            public IReadOnlyList<StackItem> LocalVariables => frame.LocalVariables;

            public IReadOnlyList<StackItem> StaticFields => frame.StaticFields;

            public IReadOnlyList<StackItem> Arguments => frame.Arguments;
        }
    }
}
