using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo.VM;
using NeoDebug.Models;
using NeoDebug.VariableContainers;
using NeoFx;
using NeoFx.Models;
using NeoFx.Storage;
using Newtonsoft.Json.Linq;
using OneOf;
using System.Collections.Generic;
using System.Linq;

namespace NeoDebug.Adapter
{
    internal class DebugExecutionEngine : ExecutionEngine, IExecutionEngine
    {
        private readonly ScriptTable scriptTable;
        private readonly EmulatedStorage storage;

        private DebugExecutionEngine(IScriptContainer container, ScriptTable scriptTable, EmulatedStorage storage, EmulatedRuntime runtime)
            : base(container, new Crypto(), scriptTable, new EmulatedInteropService(storage, runtime))
        {
            this.scriptTable = scriptTable;
            this.storage = storage;
        }

        private static IBlockchainStorage? GetBlockchain(LaunchArguments arguments)
        {
            if (arguments.ConfigurationProperties.TryGetValue("checkpoint", out var checkpoint))
            {
                return NeoFx.RocksDb.RocksDbStore.OpenCheckpoint(checkpoint.Value<string>());
            }

            return null;
        }

        static (IEnumerable<CoinReference> inputs, IEnumerable<TransactionOutput> outputs) 
            GetUtxo(LaunchArguments arguments, IBlockchainStorage? blockchain)
        {
            static UInt160 ParseAddress(string address) =>
                UInt160.TryParse(address, out var result) ? result : address.ToScriptHash();

            UInt256 GetAssetId(string asset)
            {
                if (string.Compare("neo", asset, true) == 0)
                    return blockchain.GoverningTokenHash;

                if (string.Compare("gas", asset, true) == 0)
                    return blockchain.UtilityTokenHash;

                return UInt256.Parse(asset);
            }

            if (blockchain != null 
                && arguments.ConfigurationProperties.TryGetValue("utxo", out var utxo))
            {
                var _inputs = (utxo["inputs"] ?? Enumerable.Empty<JToken>())
                    .Select(t => new CoinReference(
                        UInt256.Parse(t.Value<string>("txid")),
                        t.Value<ushort>("value")));

                var _outputs = (utxo["outputs"] ?? Enumerable.Empty<JToken>())
                    .Select(t => new TransactionOutput(
                        GetAssetId(t.Value<string>("asset")),
                        t.Value<long>("value"),
                        ParseAddress(t.Value<string>("address"))));

                return (_inputs, _outputs);
            }

            return (Enumerable.Empty<CoinReference>(), Enumerable.Empty<TransactionOutput>());
        }

        private static IEnumerable<(byte[] key, byte[] value, bool constant)> 
            GetStorage(LaunchArguments arguments)
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

            if (arguments.ConfigurationProperties.TryGetValue("storage", out var token))
            {
                return token.Select(t =>
                    (ConvertString(t["key"]),
                    ConvertString(t["value"]),
                    t.Value<bool?>("constant") ?? false));
            }

            return Enumerable.Empty<(byte[] key, byte[] value, bool constant)>();
        }

        private static EmulatedRuntime.TriggerType GetTriggerType(LaunchArguments arguments)
        {
            if (arguments.ConfigurationProperties.TryGetValue("runtime", out var token))
            {
                return "verification".Equals(token.Value<string>("trigger"))
                    ? EmulatedRuntime.TriggerType.Verification
                    : EmulatedRuntime.TriggerType.Application;
            }

            return EmulatedRuntime.TriggerType.Application;
        }

        private static OneOf<bool, IEnumerable<byte[]>> GetWitnesses(LaunchArguments arguments)
        {
            if (arguments.ConfigurationProperties.TryGetValue("runtime", out var token))
            {
                var witnessesJson = token["witnesses"];
                if (witnessesJson?.Type == JTokenType.Object)
                {
                    return witnessesJson.Value<bool>("check-result");
                }
                if (witnessesJson?.Type == JTokenType.Array)
                {
                    return OneOf<bool, IEnumerable<byte[]>>.FromT1(witnessesJson
                        .Select(t => t.Value<string>().ParseBigInteger().ToByteArray()));
                }
            }

            return true;
        }

        public static DebugExecutionEngine Create(Contract contract, LaunchArguments arguments)
        {
            var blockchain = GetBlockchain(arguments);
            var (inputs, outputs) = GetUtxo(arguments, blockchain);

            var tx = new Transaction(
                TransactionType.Invocation,
                1,
                Transaction.InvocationTxData(contract.Script, 0),
                inputs: inputs.ToArray(),
                outputs: outputs.ToArray());
            var container = new ScriptContainer<Transaction>(tx);

            var table = new ScriptTable();
            table.Add(contract);

            return new DebugExecutionEngine(container, table, null, null);
        }

        VMState IExecutionEngine.State { get => State; set { State = value; } }

        IEnumerable<StackItem> IExecutionEngine.ResultStack => ResultStack;

        ExecutionContext IExecutionEngine.CurrentContext => CurrentContext;

        RandomAccessStack<ExecutionContext> IExecutionEngine.InvocationStack => InvocationStack;

        ExecutionContext IExecutionEngine.LoadScript(byte[] script, int rvcount) => LoadScript(script, rvcount);

        void IExecutionEngine.ExecuteNext() => ExecuteNext();

        IVariableContainer IExecutionEngine.GetStorageContainer(IVariableContainerSession session) => storage.GetStorageContainer(session);
    }
}
