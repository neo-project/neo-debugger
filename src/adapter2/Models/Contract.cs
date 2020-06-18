using NeoFx;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace NeoDebug.Models
{
    public class Contract
    {
        public byte[] Script { get; }
        public UInt160 ScriptHash { get; }
        public DebugInfo DebugInfo { get; }

        public Contract(byte[] script, DebugInfo debugInfo)
        {
            Script = script;
            ScriptHash = new UInt160(Crypto.Hash160(script));
            DebugInfo = debugInfo;
        }

        public DebugInfo.Method EntryPoint => DebugInfo.Methods.Single(m => m.Id == DebugInfo.Entrypoint);

        public static async Task<Contract> Load(string vmFileName, IReadOnlyDictionary<string, string> sourceFileMap)
        {
            var scriptTask = File.ReadAllBytesAsync(vmFileName);
            var debugInfoTask = DebugInfoParser.Load(vmFileName, sourceFileMap);

            await Task.WhenAll(scriptTask, debugInfoTask).ConfigureAwait(false);

            return new Contract(scriptTask.Result, debugInfoTask.Result);
        }
    }
}
