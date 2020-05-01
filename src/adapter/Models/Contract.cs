using Neo.VM;
using NeoFx;
using System.IO;
using System.Linq;

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

        public MethodDebugInfo EntryPoint => DebugInfo.Methods.Single(m => m.Id == DebugInfo.Entrypoint);

        public byte[] BuildInvokeScript(ContractArgument[] arguments)
        {
            using var builder = new ScriptBuilder();
            for (int i = arguments.Length - 1; i >= 0; i--)
            {
                arguments[i].EmitPush(builder);
            }

            builder.EmitAppCall(ScriptHash);
            return builder.ToArray();
        }

        public static Contract Load(string vmFileName)
        {
            var script = File.ReadAllBytes(vmFileName);
            var debugInfo = DebugInfoParser.Load(vmFileName);

            return new Contract(script, debugInfo);
        }
    }
}
