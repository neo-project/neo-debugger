using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo.VM;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Neo.DebugAdapter
{
    internal class EmulatedRuntime
    {
        bool? checkWitnessBypass;
        byte[][] witnesses;

        public void RegisterServices(Action<string, Func<ExecutionEngine, bool>> register)
        {
            register(".Runtime.CheckWitness", CheckWitness);
        }

        public void BypassCheckWitness(bool value)
        {
            checkWitnessBypass = value;
            witnesses = null;
        }

        public void PopulateWitnesses(IEnumerable<byte[]> witnesses)
        {
            checkWitnessBypass = null;
            this.witnesses = witnesses.ToArray();
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
