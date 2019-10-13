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
        private readonly EmulatedStorage storage;

        private readonly UInt160 scriptHash;
        private readonly IBlockchainStorage? blockchain;
        private readonly Action<OutputEvent> sendOutput;

        //private readonly Dictionary<int, (byte[] key, byte[] value, bool constant)> storage =
        //    new Dictionary<int, (byte[] key, byte[] value, bool constant)>();

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
                return Encoding.UTF8.GetBytes(value);
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
            storage = new EmulatedStorage(blockchain);
            scriptHash = new UInt160(contract.ScriptHash);

            foreach (var item in GetStorage(config))
            {
                var storageKey = new StorageKey(scriptHash, item.key);
                storage.TryPut(storageKey, item.value, item.constant);
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

            RegisterAccount(Register);
            RegisterAsset(Register);
            RegisterBlock(Register);
            RegisterBlockchain(Register);
            RegisterContract(Register);
            RegisterEnumerator(Register);
            RegisterExecutionEngine(Register);
            RegisterHeader(Register);
            RegisterRuntime(Register);
            RegisterStorage(Register);
            RegisterTransaction(Register);
        }

        internal IVariableContainer GetStorageContainer(IVariableContainerSession session)
        {
            return new EmulatedStorageContainer(session, scriptHash, storage);
        }

        protected void Register(string methodName, Func<ExecutionEngine, bool> handler, int price)
        {
            if (HashHelpers.TryInteropMethodHash(methodName, out var value))
            {
                methods.Add(value, handler);
            }

            throw new ArgumentException(nameof(methodName));
        }

        bool IInteropService.Invoke(byte[] method, ExecutionEngine engine)
        {
            static bool TryInteropMethodHash(Span<byte> method, out uint value)
            {
                if (method.Length == 4)
                {
                    value = BitConverter.ToUInt32(method);
                    return true; 
                }

                return HashHelpers.TryInteropMethodHash(method, out value);
            }

            if (TryInteropMethodHash(method, out var hash)
                && methods.TryGetValue(hash, out var func))
            {
                try
                {
                    return func(engine);
                }
                catch (Exception ex)
                {
                    sendOutput(new OutputEvent()
                    {
                        Category = OutputEvent.CategoryValue.Stderr,
                        Output = ex.ToString(),
                    });
                }
            }

            return false;
        }
    }
}
