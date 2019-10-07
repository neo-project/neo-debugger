using Neo.VM;
using NeoFx;
using NeoFx.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace NeoDebug.Adapter
{
    internal partial class InteropService
    {
        public void RegisterOutput(Action<string, Func<ExecutionEngine, bool>, int> register)
        {
            register("Neo.Output.GetAssetId", Output_GetAssetId, 1);
            register("Neo.Output.GetValue", Output_GetValue, 1);
            register("Neo.Output.GetScriptHash", Output_GetScriptHash, 1);

            register("AntShares.Output.GetAssetId", Output_GetAssetId, 1);
            register("AntShares.Output.GetValue", Output_GetValue, 1);
            register("AntShares.Output.GetScriptHash", Output_GetScriptHash, 1);
        }

        private bool Output_GetAssetId(ExecutionEngine engine)
        {
            var evalStack = engine.CurrentContext.EvaluationStack;

            if (evalStack.TryPopContainedStruct<TransactionOutput>(out var output)
                && output.AssetId.TryToArray(out var array))
            {
                evalStack.Push(array);
                return true;
            }

            return false;
        }

        private bool Output_GetValue(ExecutionEngine engine)
        {
            var evalStack = engine.CurrentContext.EvaluationStack;

            if (evalStack.TryPopContainedStruct<TransactionOutput>(out var output))
            {
                evalStack.Push(output.Value);
                return true;
            }

            return false;
        }

        private bool Output_GetScriptHash(ExecutionEngine engine)
        {
            var evalStack = engine.CurrentContext.EvaluationStack;

            if (evalStack.TryPopContainedStruct<TransactionOutput>(out var output)
                && output.ScriptHash.TryToArray(out var array))
            {
                evalStack.Push(array);
                return true;
            }

            return false;
        }
    }
}
