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

namespace NeoDebug
{
    class DebugExecutionEngine : ExecutionEngine
    {
        private readonly InteropService interopService;

        private DebugExecutionEngine(IScriptContainer container, ScriptTable scriptTable, InteropService interopService)
            : base(container, new Crypto(), scriptTable, interopService)
        {
            this.interopService = interopService;
        }

        private static IBlockchainStorage? GetBlockchain(Dictionary<string, JToken> config)
        {
            if (config.TryGetValue("checkpoint", out var checkpoint))
            {
                return NeoFx.RocksDb.RocksDbStore.OpenCheckpoint(checkpoint.Value<string>());
            }

            return null;
        }

        static (IEnumerable<CoinReference> inputs, IEnumerable<TransactionOutput> outputs)
            GetUtxo(Dictionary<string, JToken> config, IBlockchainStorage? blockchain)
        {
            static UInt160 ParseAddress(string address) =>
                UInt160.TryParse(address, out var result) ? result : address.ToScriptHash();

            static UInt256 GetAssetId(IBlockchainStorage blockchain, string asset)
            {
                if (string.Compare("neo", asset, true) == 0)
                    return blockchain.GoverningTokenHash;

                if (string.Compare("gas", asset, true) == 0)
                    return blockchain.UtilityTokenHash;

                return UInt256.Parse(asset);
            }

            if (blockchain != null && config.TryGetValue("utxo", out var utxo))
            {
                var _inputs = (utxo["inputs"] ?? Enumerable.Empty<JToken>())
                    .Select(t => new CoinReference(
                        UInt256.Parse(t.Value<string>("txid")),
                        t.Value<ushort>("value")));

                var _outputs = (utxo["outputs"] ?? Enumerable.Empty<JToken>())
                    .Select(t => new TransactionOutput(
                        GetAssetId(blockchain, t.Value<string>("asset")),
                        Fixed8.Create(t.Value<long>("value")),
                        ParseAddress(t.Value<string>("address"))));

                return (_inputs, _outputs);
            }

            return (Enumerable.Empty<CoinReference>(), Enumerable.Empty<TransactionOutput>());
        }

        public static DebugExecutionEngine Create(Contract contract, LaunchArguments arguments, Action<OutputEvent> sendOutput)
        {
            var blockchain = GetBlockchain(arguments.ConfigurationProperties);
            var (inputs, outputs) = GetUtxo(arguments.ConfigurationProperties, blockchain);

            var tx = new InvocationTransaction(contract.Script, Fixed8.Zero, 1, default,
                inputs.ToArray(), outputs.ToArray(), default);
            var container = new ModelAdapters.TransactionAdapter(tx);

            var table = new ScriptTable();
            table.Add(contract.Script);

            //TODO: load these from launch config
            var emulatedStorage = new EmulatedStorage(null, Enumerable.Empty<(StorageKey, StorageItem)>());
            var witnessChecker = new WitnessChecker(true);
            var events = Enumerable.Empty<(UInt160, string, EventDebugInfo)>();

            var interopService = new InteropService(null, emulatedStorage, TriggerType.Application, witnessChecker, sendOutput, events); 
            return new DebugExecutionEngine(container, table, interopService);
        }

        public void ExecuteInstruction() => ExecuteNext();

        public IVariableContainer GetStorageContainer(IVariableContainerSession session, byte[] scriptHash)
            => GetStorageContainer(session, new UInt160(scriptHash));

        public IVariableContainer GetStorageContainer(IVariableContainerSession session, UInt160 scriptHash)
            => interopService.GetStorageContainer(session, scriptHash);

        public EvaluateResponse EvaluateStorageExpression(IVariableContainerSession session, EvaluateArguments args)
            => interopService.EvaluateStorageExpression(session, args);

        public string GetMethodName(uint methodHash) => interopService.GetMethodName(methodHash);
    }
}
