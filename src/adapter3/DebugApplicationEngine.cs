using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Neo.Cryptography;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.SmartContract;
using StackItem = Neo.VM.Types.StackItem;
using NeoArray = Neo.VM.Types.Array;
using Neo;
using System.Numerics;
using System.Linq;

namespace NeoDebug.Neo3
{
    using ServiceMethod = Func<DebugApplicationEngine, IReadOnlyList<InteropParameterDescriptor>, StackItem?>;

    class DebugApplicationEngine : ApplicationEngine
    {
        readonly static IReadOnlyDictionary<uint, ServiceMethod> debugServices;

        static DebugApplicationEngine()
        {
            var debugServices = new Dictionary<uint, ServiceMethod>();

            Register("System.Runtime.CheckWitness", Debug_CheckWitness);
            Register("System.Blockchain.GetBlock", Debug_GetBlock);
            Register("System.Blockchain.GetTransactionFromBlock", Debug_GetTransactionFromBlock);
            Register("System.Runtime.Log", Debug_RuntimeLog);
            Register("System.Runtime.Notify", Debug_RuntimeNotify);

            DebugApplicationEngine.debugServices = debugServices;

            void Register(string name, ServiceMethod method)
            {
                var hash = BitConverter.ToUInt32(Encoding.ASCII.GetBytes(name).Sha256(), 0);
                debugServices.Add(hash, method);
            }
        }

        public event EventHandler<NotifyEventArgs>? DebugNotify;
        public event EventHandler<LogEventArgs>? DebugLog;
        private readonly WitnessChecker witnessChecker;
        private readonly IReadOnlyDictionary<uint, UInt256> blockHashMap;

        public DebugApplicationEngine(IVerifiable container, StoreView storeView, WitnessChecker witnessChecker) : base(TriggerType.Application, container, storeView, 0, true)
        {
            this.witnessChecker = witnessChecker;
            this.blockHashMap = storeView.Blocks.Find()
                .ToDictionary(
                    t => t.Value.Index, 
                    t => t.Value.Hash == t.Key ? t.Key : throw new Exception("invalid hash"));
        }

        public void ExecuteInstruction() => ExecuteNext();

        protected override void OnSysCall(uint methodHash)
        {
            if (debugServices.TryGetValue(methodHash, out var method))
            {
                InteropDescriptor descriptor = Services[methodHash];
                ValidateCallFlags(descriptor);
                AddGas(descriptor.FixedPrice);

                var result = method(this, descriptor.Parameters);
                if (result != null)
                {
                    Push(result);
                }
            }
            else
            {
                base.OnSysCall(methodHash);
            }
        }

        private static StackItem? Debug_CheckWitness(
            DebugApplicationEngine engine,
            IReadOnlyList<InteropParameterDescriptor> paramDescriptors)
        {
            Debug.Assert(paramDescriptors.Count == 1);
            _ = (byte[])engine.Convert(engine.Pop(), paramDescriptors[0]);

            return engine.witnessChecker.Check(Neo.UInt160.Zero);
        }

        private static StackItem? Debug_RuntimeNotify(
            DebugApplicationEngine engine,
            IReadOnlyList<InteropParameterDescriptor> paramDescriptors)
        {
            Debug.Assert(paramDescriptors.Count == 2);

            var eventName = (byte[])engine.Convert(engine.Pop(), paramDescriptors[0]);
            var state = (NeoArray)(byte[])engine.Convert(engine.Pop(), paramDescriptors[1]);

            NotifyEventArgs args = new NotifyEventArgs(
                engine.ScriptContainer,
                engine.CurrentScriptHash,
                Neo.Utility.StrictUTF8.GetString(eventName), 
                (NeoArray)state.DeepCopy());
            engine.DebugNotify?.Invoke(engine, args);

            engine.RuntimeNotify(eventName, state);
            return null;
        }

        private static StackItem? Debug_RuntimeLog(
            DebugApplicationEngine engine,
            IReadOnlyList<InteropParameterDescriptor> paramDescriptors)
        {
            Debug.Assert(paramDescriptors.Count == 1);

            var state = (byte[])engine.Convert(engine.Pop(), paramDescriptors[0]);
            var args = new LogEventArgs(
                engine.ScriptContainer,
                engine.CurrentScriptHash,
                Neo.Utility.StrictUTF8.GetString(state));
            engine.DebugLog?.Invoke(engine, args);

            engine.RuntimeLog(state);
            return null;
        }

        private static StackItem? Debug_GetBlock(
            DebugApplicationEngine engine,
            IReadOnlyList<InteropParameterDescriptor> paramDescriptors)
        {
            Debug.Assert(paramDescriptors.Count == 1);

            var indexOrHash = (byte[])engine.Convert(engine.Pop(), paramDescriptors[0]); 
            var hash = engine.GetBlockHash(indexOrHash);

            if (hash is null) return StackItem.Null;
            Block block = engine.Snapshot.GetBlock(hash);
            if (block is null) return StackItem.Null;
            return block.ToStackItem(engine.ReferenceCounter);
        }

        private static StackItem? Debug_GetTransactionFromBlock(
            DebugApplicationEngine engine,
            IReadOnlyList<InteropParameterDescriptor> paramDescriptors)
        {
            Debug.Assert(paramDescriptors.Count == 2);

            var blockIndexOrHash = (byte[])engine.Convert(engine.Pop(), paramDescriptors[0]);
            var txIndex = (int)engine.Convert(engine.Pop(), paramDescriptors[1]);

            var hash = engine.GetBlockHash(blockIndexOrHash);

            if (hash is null) return StackItem.Null;
            var block = engine.Snapshot.Blocks.TryGet(hash);
            if (block is null) return StackItem.Null;
            if (txIndex < 0 || txIndex >= block.Hashes.Length - 1)
                throw new ArgumentOutOfRangeException(nameof(txIndex));
            return engine.Snapshot.GetTransaction(block.Hashes[txIndex + 1])
                .ToStackItem(engine.ReferenceCounter);
        }

        private UInt256? GetBlockHash(byte[] indexOrHash)
        {
            if (indexOrHash.Length == UInt256.Length)
            {
                return new UInt256(indexOrHash);
            }

            if (indexOrHash.Length < UInt256.Length)
            {
                var index = new BigInteger(indexOrHash);
                if (index < uint.MinValue || index > uint.MaxValue)
                    throw new ArgumentOutOfRangeException(nameof(indexOrHash));
                if (blockHashMap.TryGetValue((uint)index, out var hash))
                {
                    return hash;
                }
            }

            throw new ArgumentException();
        }
    }
}
