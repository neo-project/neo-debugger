using Neo.VM;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Neo.DebugAdapter
{
    class Contract
    {
        public byte[] Script { get; }
        public AbiInfo AbiInfo { get; }
        public DebugInfo DebugInfo { get; }

        public byte[] ScriptHash { get; }

        public Contract(byte[] script, AbiInfo abiInfo, DebugInfo debugInfo)
        {
            Script = script;
            ScriptHash = Crypto.Hash160(script);
            AbiInfo = abiInfo;
            DebugInfo = debugInfo;
        }

        public Function EntryPoint => AbiInfo.Functions.Single(f => f.Name == AbiInfo.Entrypoint);

        public static IEnumerable<ContractArgument> ParseArguments(Function function, JToken args)
        {
            return args
                .Select(j => j.Value<string>())
                .Zip(function.Parameters, (a, p) => ContractArgument.FromArgument(p.Type, a));
        }

        public ScriptBuilder BuildInvokeScript(ContractArgument[] arguments)
        {
            var builder = new ScriptBuilder();
            for (var x = 0; x < arguments.Length; x++)
            {
                arguments[x].EmitPush(builder);
            }
            builder.EmitAppCall(ScriptHash);
            return builder;
        }

        public static Contract Load(string vmFileName)
        {
            if (!File.Exists(vmFileName))
                throw new ArgumentException($"{nameof(vmFileName)} file doesn't exist");

            var debugJsonFileName = Path.ChangeExtension(vmFileName, ".debug.json");
            if (!File.Exists(debugJsonFileName))
                throw new ArgumentException($"{nameof(vmFileName)} debug info file doesn't exist");

            var abiJsonFileName = Path.ChangeExtension(vmFileName, ".abi.json");
            if (!File.Exists(abiJsonFileName))
                throw new ArgumentException($"{nameof(vmFileName)} ABI info file doesn't exist");

            var script = File.ReadAllBytes(vmFileName);
            var abiInfo = AbiInfo.FromJson(File.ReadAllText(abiJsonFileName));
            var debugInfo = DebugInfo.FromJson(File.ReadAllText(debugJsonFileName));

            return new Contract(script, abiInfo, debugInfo);
        }
    }
}
