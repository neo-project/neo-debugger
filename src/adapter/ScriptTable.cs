using NeoDebug.Models;
using System;
using System.Collections.Generic;

namespace NeoDebug
{
    internal class ScriptTable : Neo.VM.IScriptTable
    {
        private readonly Dictionary<int, byte[]> scripts = new Dictionary<int, byte[]>();

        public void Add(byte[] script) 
            => scripts.Add(Crypto.Hash160(script).GetSequenceHashCode(), script);

        public byte[] GetScript(byte[] scriptHash)
            => GetScript(scriptHash.GetSequenceHashCode());

        public byte[] GetScript(int sourceReference)
            => scripts.TryGetValue(sourceReference, out var script)
                ? script : Array.Empty<byte>();
    }
}
