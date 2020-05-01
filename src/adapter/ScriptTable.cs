using NeoDebug.Models;
using System;
using System.Collections.Generic;

namespace NeoDebug
{
    internal class ScriptTable : Neo.VM.IScriptTable
    {
        private readonly Dictionary<int, Neo.VM.Script> scripts = new Dictionary<int, Neo.VM.Script>();

        public void Add(byte[] script)
        {
            var scriptObj = new Neo.VM.Script(Crypto.Default, script);
            scripts.Add(GetHashCode(scriptObj.ScriptHash), scriptObj);
        }

        // Inspired by https://stackoverflow.com/a/7244522
        public static int GetHashCode(Span<byte> span)
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

        public byte[] GetScript(byte[] scripthash) 
            => GetScript(GetHashCode(scripthash));

        public byte[] GetScript(int sourceReference) 
            => scripts.TryGetValue(sourceReference, out var script)
                ? script : Array.Empty<byte>();

    }
}
