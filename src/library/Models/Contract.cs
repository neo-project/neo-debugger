using Neo.VM;
using System;
using System.IO;
using System.Linq;

namespace NeoDebug.Models
{
    public class Contract
    {
        public byte[] Script { get; }
        public DebugInfo DebugInfo { get; }
        public byte[] ScriptHash { get; }

        public Contract(byte[] script, DebugInfo debugInfo, Func<byte[], byte[]> scriptHashFunc)
        {
            Script = script;
            ScriptHash = scriptHashFunc(script);
            DebugInfo = debugInfo;
        }

        public MethodDebugInfo EntryPoint => DebugInfo.Methods.Single(m => m.Id == DebugInfo.Entrypoint);

        public ScriptBuilder BuildInvokeScript(ContractArgument[] arguments)
        {
            var builder = new ScriptBuilder();
            for (int i = arguments.Length - 1; i >= 0; i--)
            {
                arguments[i].EmitPush(builder);
            }
            builder.EmitAppCall(ScriptHash);
            return builder;
        }

        public static Contract Load(string vmFileName, Func<byte[], byte[]> scriptHashFunc)
        {
            var script = File.ReadAllBytes(vmFileName);
            var debugInfo = DebugInfoParser.Load(vmFileName);

            return new Contract(script, debugInfo, scriptHashFunc);
        }
    }
}
