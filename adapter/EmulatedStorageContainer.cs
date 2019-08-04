using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo.VM;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Neo.DebugAdapter
{
    internal class EmulatedStorageContainer : IVariableContainer
    {
        private readonly NeoDebugSession session;
        private readonly EmulatedStorage storage;

        public EmulatedStorageContainer(NeoDebugSession session, EmulatedStorage storage)
        {
            this.storage = storage;
            this.session = session;
        }

        public IEnumerable<Variable> GetVariables(VariablesArguments args)
        {
            foreach (var kvp in storage.Storage)
            {
                var neoByteArray = new Neo.VM.Types.ByteArray(kvp.Value);
                yield return neoByteArray.GetVariable(session, kvp.Key.ToString());
            }
        }
    }
}
