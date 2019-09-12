using Neo.VM;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NeoDebug.Adapter
{
    internal class EmulatedRuntime
    {
        bool? checkWitnessBypass;
        byte[][] witnesses;

        public void RegisterServices(Action<string, Func<ExecutionEngine, bool>> register)
        {
            register("System.Runtime.CheckWitness", CheckWitness);
            register("Neo.Runtime.CheckWitness", CheckWitness);
            register("AntShares.Runtime.CheckWitness", CheckWitness);
        }

        public EmulatedRuntime(bool value)
        {
            checkWitnessBypass = value;
            witnesses = null;
        }

        public EmulatedRuntime(IEnumerable<byte[]> witnesses = null)
        {
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
    }
}
