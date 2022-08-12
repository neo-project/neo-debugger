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

            public ExecutionContextAdapter(ExecutionContext context, IDictionary<UInt160, UInt160> scriptIdMap)
            {
                this.context = context;
                // this.ScriptHash = context.GetScriptHash();

                // if (scriptIdMap.TryGetValue(context.GetScriptHash(), out var scriptHash))
                // {
                //     this.ScriptIdentifier = scriptHash;
                // }
                // else
                // {
                //     this.ScriptIdentifier = Neo.SmartContract.Helper.ToScriptHash(context.Script);
                //     scriptIdMap[this.ScriptHash] = this.ScriptIdentifier;
                // }
            }

            public Instruction? CurrentInstruction => context.CurrentInstruction;

            public int InstructionPointer => context.InstructionPointer;

            public IReadOnlyList<StackItem> EvaluationStack => context.EvaluationStack;

            public IReadOnlyList<StackItem> LocalVariables => Coalese(context.LocalVariables);

            public IReadOnlyList<StackItem> StaticFields => Coalese(context.StaticFields);

            public IReadOnlyList<StackItem> Arguments => Coalese(context.Arguments);

            public Script Script => context.Script;
            public IReadOnlyList<MethodToken> Tokens => context.GetState<ExecutionContextState>()?.Contract?.Nef?.Tokens
                ?? Array.Empty<MethodToken>();

            public uint? NefChecksum => context.GetState<ExecutionContextState>()?.Contract?.Nef.CheckSum;

            public UInt160 ScriptHash => context.GetScriptHash();
            // public UInt160 ScriptIdentifier { get; }

            static IReadOnlyList<StackItem> Coalese(Neo.VM.Slot? slot) => (slot == null) ? Array.Empty<StackItem>() : slot;
        }

    }
}
