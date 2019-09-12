using NeoDebug.Models;
using System.Collections.Generic;

namespace NeoDebug.Adapter
{
    internal class ScriptTable : Neo.VM.IScriptTable
    {
        private readonly Dictionary<int, byte[]> scripts = new Dictionary<int, byte[]>();

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
