using Neo.VM;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NeoDebug.Adapter
{
    internal class EmulatedRuntime
    {
        public enum TriggerType
        {
            Verification = 0x00,
            Application = 0x10,
        }

        private readonly bool? checkWitnessBypass;
        private readonly byte[][] witnesses;
        private readonly TriggerType trigger;

        public void RegisterServices(Action<string, Func<ExecutionEngine, bool>> register)
        {
            register("System.Runtime.CheckWitness", CheckWitness);
            register("Neo.Runtime.CheckWitness", CheckWitness);
            register("AntShares.Runtime.CheckWitness", CheckWitness);

            register("System.Runtime.GetTrigger", GetTrigger);
            register("Neo.Runtime.GetTrigger", GetTrigger);
        }

        public EmulatedRuntime()
            : this(TriggerType.Application, true)
        {
        }

        public EmulatedRuntime(TriggerType triggerType, bool value)
        {
            trigger = triggerType;
            checkWitnessBypass = value;
            witnesses = null;
        }

        public EmulatedRuntime(TriggerType triggerType, IEnumerable<byte[]> witnesses)
        {
            trigger = triggerType;
            checkWitnessBypass = null;
            this.witnesses = (witnesses ?? Enumerable.Empty<byte[]>()).ToArray();
        }

        private bool CheckWitness(ExecutionEngine engine)
        {
            var evalStack = engine.CurrentContext.EvaluationStack;
            var hash = evalStack.Pop().GetByteArray();

            if (checkWitnessBypass.HasValue)
            {
                evalStack.Push(checkWitnessBypass.Value);
                return true;
            }
            else
            {
                var hashSpan = hash.AsSpan();
                for (int i = 0; i < witnesses.Length; i++)
                {
                    if (hashSpan.SequenceEqual(witnesses[i]))
                    {
                        evalStack.Push(true);
                        return true;
                    }
                }

                evalStack.Push(false);
                return true;
            }
        }

        private bool GetTrigger(ExecutionEngine engine)
        {
            engine.CurrentContext.EvaluationStack.Push((int)trigger);
            return true;
        }
    }
}
