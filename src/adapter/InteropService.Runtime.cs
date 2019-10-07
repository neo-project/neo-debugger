using Neo.VM;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

#nullable enable

namespace NeoDebug.Adapter
{
    internal partial class InteropService
    {
        private readonly TriggerType trigger = TriggerType.Application;
        private readonly bool checkWitnessBypass = false;
        private readonly bool checkWitnessBypassValue;
        private readonly IEnumerable<byte[]> witnesses = Enumerable.Empty<byte[]>();

        private void RegisterRuntime(Action<string, Func<ExecutionEngine, bool>, int> register)
        {
            register("Neo.Runtime.GetTrigger", Runtime_GetTrigger, 1);
            register("Neo.Runtime.CheckWitness", Runtime_CheckWitness, 200);
            register("Neo.Runtime.Notify", Runtime_Notify, 1);
            register("Neo.Runtime.Log", Runtime_Log, 1);
            register("Neo.Runtime.GetTime", Runtime_GetTime, 1);
            register("Neo.Runtime.Serialize", Runtime_Serialize, 1);
            register("Neo.Runtime.Deserialize", Runtime_Deserialize, 1);

            register("System.Runtime.Platform", Runtime_Platform, 1);
            register("System.Runtime.GetTrigger", Runtime_GetTrigger, 1);
            register("System.Runtime.CheckWitness", Runtime_CheckWitness, 200);
            register("System.Runtime.Notify", Runtime_Notify, 1);
            register("System.Runtime.Log", Runtime_Log, 1);
            register("System.Runtime.GetTime", Runtime_GetTime, 1);
            register("System.Runtime.Serialize", Runtime_Serialize, 1);
            register("System.Runtime.Deserialize", Runtime_Deserialize, 1);

            register("AntShares.Runtime.CheckWitness", Runtime_CheckWitness, 200);
            register("AntShares.Runtime.Notify", Runtime_Notify, 1);
            register("AntShares.Runtime.Log", Runtime_Log, 1);
        }

        private bool Runtime_Platform(ExecutionEngine engine)
        {
            throw new NotImplementedException(nameof(Runtime_Platform));
        }

        private bool Runtime_Deserialize(ExecutionEngine engine)
        {
            throw new NotImplementedException(nameof(Runtime_Deserialize));
        }

        private bool Runtime_Serialize(ExecutionEngine engine)
        {
            throw new NotImplementedException(nameof(Runtime_Serialize));
        }

        private bool Runtime_GetTime(ExecutionEngine engine)
        {
            throw new NotImplementedException(nameof(Runtime_GetTime));
        }

        private bool Runtime_Log(ExecutionEngine engine)
        {
            throw new NotImplementedException(nameof(Runtime_Log));
        }

        private bool Runtime_Notify(ExecutionEngine engine)
        {
            throw new NotImplementedException(nameof(Runtime_Notify));
        }

        private bool Runtime_CheckWitness(ExecutionEngine engine)
        {
            var evalStack = engine.CurrentContext.EvaluationStack;
            var hash = evalStack.Pop().GetByteArray();

            if (checkWitnessBypass)
            {
                evalStack.Push(checkWitnessBypassValue);
                return true;
            }
            else
            {
                var hashSpan = hash.AsSpan();
                foreach (var witness in witnesses)
                {
                    if (hashSpan.SequenceEqual(witness))
                    {
                        evalStack.Push(true);
                        return true;
                    }
                }

                evalStack.Push(false);
                return true;
            }
        }

        private bool Runtime_GetTrigger(ExecutionEngine engine)
        {
            engine.CurrentContext.EvaluationStack.Push((int)trigger);
            return true;
        }
    }
}
