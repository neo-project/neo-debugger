using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Linq;

namespace Neo.DebugAdapter
{
    class Contract
    {
        public byte[] Script;
        public SequencePoint[] SequencePoints;
        public string EntryPoint;
        public ContractFunction[] Functions;

        public byte[] ScriptHash => Crypto.Hash160(Script);

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
            var sequencePoints = JArray.Parse(File.ReadAllText(debugJsonFileName)).Select(SequencePoint.FromJson);

            var abiJson = JObject.Parse(File.ReadAllText(abiJsonFileName));
            var entrypoint = abiJson.Value<string>("entrypoint");
            var functions = abiJson["functions"].Select(ContractFunction.FromJson);

            return new Contract()
            {
                Script = script,
                SequencePoints = sequencePoints.ToArray(),
                EntryPoint = entrypoint,
                Functions = functions.ToArray()
            };
        }
    }
}
