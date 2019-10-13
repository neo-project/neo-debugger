using NeoDebug.Models;
using System;
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

        // Inspired by https://stackoverflow.com/a/7244522
        private static int GetHashCode(Span<byte> span)
        {
            unchecked
            {
                int hash = 17;
                for (int i = 0; i < span.Length; i++)
                {
                    hash = (hash * 23) + span[i];
                }
                return hash;
            }
        }

        public void Add(byte[] key, byte[] script)
        {
            scripts[GetHashCode(key)] = script;
        }

        public byte[] GetScript(byte[] key)
        {
            return scripts[GetHashCode(key)];
        }
    }
}
