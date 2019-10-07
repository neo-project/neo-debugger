using Neo.VM;
using System;
using System.Collections.Generic;
using System.Text;

namespace NeoDebug.Adapter
{
    internal partial class InteropService
    {
        private void RegisterExecutionEngine(Action<string, Func<ExecutionEngine, bool>, int> register)
        {
            register("System.ExecutionEngine.GetScriptContainer", ExecutionEngine_GetScriptContainer, 1);
            register("System.ExecutionEngine.GetExecutingScriptHash", ExecutionEngine_GetExecutingScriptHash, 1);
            register("System.ExecutionEngine.GetCallingScriptHash", ExecutionEngine_GetCallingScriptHash, 1);
            register("System.ExecutionEngine.GetEntryScriptHash", ExecutionEngine_GetEntryScriptHash, 1);
        }

        private bool ExecutionEngine_GetEntryScriptHash(ExecutionEngine engine)
        {
            engine.CurrentContext.EvaluationStack.Push(engine.EntryContext.ScriptHash);
            return true;
        }

        private bool ExecutionEngine_GetCallingScriptHash(ExecutionEngine engine)
        {
            engine.CurrentContext.EvaluationStack.Push(engine.CallingContext?.ScriptHash ?? Array.Empty<byte>());
            return true;
        }

        private bool ExecutionEngine_GetExecutingScriptHash(ExecutionEngine engine)
        {
            engine.CurrentContext.EvaluationStack.Push(engine.CurrentContext.ScriptHash);
            return true;
        }

        private bool ExecutionEngine_GetScriptContainer(ExecutionEngine engine)
        {
            // TODO stack item
            engine.CurrentContext.EvaluationStack.Push(StackItem.FromInterface(engine.ScriptContainer));
            return true;
        }
    }
}