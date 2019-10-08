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
        public void RegisterInput(Action<string, Func<ExecutionEngine, bool>, int> register)
        {
            register("Neo.Input.GetHash", Input_GetHash, 1);
            register("Neo.Input.GetIndex", Input_GetIndex, 1);

            register("AntShares.Input.GetHash", Input_GetHash, 1);
            register("AntShares.Input.GetIndex", Input_GetIndex, 1);
        }

        private bool Input_GetIndex(ExecutionEngine engine)
        {
            var evalStack = engine.CurrentContext.EvaluationStack;

            if (evalStack.TryPopContainedStruct<CoinReference>(out var input))
            {
                evalStack.Push((int)input.PrevIndex);
                return true;
            }

            return false;
        }

        private bool Input_GetHash(ExecutionEngine engine)
        {
            var evalStack = engine.CurrentContext.EvaluationStack;

            if (evalStack.TryPopContainedStruct<CoinReference>(out var output)
                && output.PrevHash.TryToArray(out var array))
            {
                evalStack.Push(array);
                return true;
            }

            return false;
        }
    }
}
