using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo.VM;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Neo.DebugAdapter
{
    internal class ArrayContainer : IVariableContainer
    {
        private readonly NeoDebugSession session;
        private readonly Neo.VM.Types.Array array;

        public ArrayContainer(NeoDebugSession session, Neo.VM.Types.Array array)
        {
            this.session = session;
            this.array = array;
        }

        public IEnumerable<Variable> GetVariables(VariablesArguments args)
        {
            for (int i = 0; i < array.Count; i++)
            {
                var variable = array[i].GetVariable(session);
                variable.Name = i.ToString();
                yield return variable;
            }
        }
    }
}
