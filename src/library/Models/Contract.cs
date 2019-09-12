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

        public Method EntryPoint => DebugInfo.Methods.Single(m => m.Name == DebugInfo.Entrypoint);

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
            if (!File.Exists(vmFileName))
                throw new ArgumentException($"{nameof(vmFileName)} file doesn't exist");

            var debugJsonFileName = Path.ChangeExtension(vmFileName, ".debug.json");
            if (!File.Exists(debugJsonFileName))
                throw new ArgumentException($"{nameof(vmFileName)} debug info file doesn't exist");

            var script = File.ReadAllBytes(vmFileName);
            var debugInfo = DebugInfo.FromJson(File.ReadAllText(debugJsonFileName));

            return new Contract(script, debugInfo, scriptHashFunc);
        }
    }
}
