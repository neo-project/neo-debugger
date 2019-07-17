using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Neo.DebugAdapter
{
    class ScriptTable : Neo.VM.IScriptTable
    {
        // Note, byte arrays have reference semantics for GetHashCode
        // so I create a BigInteger from the key byte array to use as 
        // the dictionary key.
        Dictionary<int, byte[]> scripts = new Dictionary<int, byte[]>();

        public void Add(Contract contract)
        {
            Add(contract.ScriptHash, contract.Script);
        }

        public void Add(byte[] key, byte[] script)
        {
            scripts[Crypto.GetHashCode(key)] = script;
        }

        public byte[] GetScript(byte[] key)
        {
            return scripts[Crypto.GetHashCode(key)];
        }
    }
}
