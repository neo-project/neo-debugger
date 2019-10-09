using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo.VM;
using NeoDebug.Models;
using NeoDebug.VariableContainers;
using NeoFx;
using NeoFx.Models;
using NeoFx.Storage;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

#nullable enable

namespace NeoDebug.Adapter
{
    internal partial class InteropService : IInteropService
    {
        public enum TriggerType
        {
            Verification = 0x00,
            Application = 0x10,
        }

        private readonly Dictionary<uint, Func<ExecutionEngine, bool>> methods = new Dictionary<uint, Func<ExecutionEngine, bool>>();

        private readonly IBlockchainStorage? blockchain;
        private readonly Action<OutputEvent> sendOutput;
        private readonly Dictionary<int, (byte[] key, byte[] value, bool constant)> storage =
            new Dictionary<int, (byte[] key, byte[] value, bool constant)>();

        private static IEnumerable<(byte[] key, byte[] value, bool constant)>
            GetStorage(Dictionary<string, JToken> config)
        {
            static byte[] ConvertString(JToken token)
            {
                var value = token.Value<string>();
                if (value.TryParseBigInteger(out var bigInteger))
                {
                    return bigInteger.ToByteArray();
                }
                return System.Text.Encoding.UTF8.GetBytes(value);
            }

            if (config.TryGetValue("storage", out var token))
            {
                return token.Select(t =>
                    (ConvertString(t["key"]),
                    ConvertString(t["value"]),
                    t.Value<bool?>("constant") ?? false));
            }

            return Enumerable.Empty<(byte[], byte[], bool)>();
        }

        public InteropService(Contract contract, IBlockchainStorage? blockchain, Dictionary<string, JToken> config, Action<OutputEvent> sendOutput)
        {
            static byte[] ParseWitness(JToken value)
            {
                if (value.Value<string>().TryParseBigInteger(out var bigInt))
                {
                    return bigInt.ToByteArray();
                }

                throw new Exception($"TryParseBigInteger for {value} failed");
            }

            this.sendOutput = sendOutput;
            this.blockchain = blockchain;

            foreach (var item in GetStorage(config))
            {
                var storageHash = StorageContext.GetHashCode(contract.ScriptHash, item.key);
                storage[storageHash] = item;
            }

            if (config.TryGetValue("runtime", out var token))
            {
                trigger = "verification".Equals(token.Value<string>("trigger"), StringComparison.InvariantCultureIgnoreCase)
                    ? TriggerType.Verification : TriggerType.Application;

                var witnessesJson = token["witnesses"];
                if (witnessesJson?.Type == JTokenType.Object)
                {
                    checkWitnessBypass = true;
                    checkWitnessBypassValue = witnessesJson.Value<bool>("check-result");
                }
                else if (witnessesJson?.Type == JTokenType.Array)
                {
                    checkWitnessBypass = false;
                    witnesses = witnessesJson.Select(ParseWitness);
                }
            }

            RegisterBlock(Register);
            RegisterBlockchain(Register);
            RegisterExecutionEngine(Register);
            RegisterInput(Register);
            RegisterOutput(Register);
            RegisterRuntime(Register);
            RegisterStorage(Register);
            RegisterTransaction(Register);
        }

        protected void Register(string methodName, Func<ExecutionEngine, bool> handler, int price)
        {
            methods.Add(InteropMethodHash(methodName), handler);
        }

        bool IInteropService.Invoke(byte[] method, ExecutionEngine engine)
        {
            uint hash = method.Length == 4
               ? BitConverter.ToUInt32(method, 0)
               : InteropMethodHash(method);

            if (methods.TryGetValue(hash, out var func))
            {
                return func(engine);
            }

            return false;
        }

        private static uint InteropMethodHash(byte[] asciiMethodName)
        {
            var hash = Crypto.SHA256.Value.ComputeHash(asciiMethodName);
            return BitConverter.ToUInt32(hash, 0);
        }

        private static uint InteropMethodHash(string methodName)
        {
            return InteropMethodHash(Encoding.ASCII.GetBytes(methodName));
        }
    }
}
