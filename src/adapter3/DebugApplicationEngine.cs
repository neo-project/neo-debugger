using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Neo.Cryptography;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.SmartContract;
using StackItem = Neo.VM.Types.StackItem;
using Neo;
using System.Numerics;
using System.Linq;
using Neo.VM;
using System.Diagnostics.CodeAnalysis;
using NeoArray = Neo.VM.Types.Array;
using Neo.Cryptography.ECC;

namespace NeoDebug.Neo3
{
    using ServiceMethod = Func<DebugApplicationEngine, IReadOnlyList<InteropParameterDescriptor>, StackItem?>;

    internal partial class DebugApplicationEngine : ApplicationEngine, IApplicationEngine
    {
        private readonly static IReadOnlyDictionary<uint, ServiceMethod> debugServices;

        static DebugApplicationEngine()
        {
            var debugServices = new Dictionary<uint, ServiceMethod>();

            Register("System.Runtime.CheckWitness", Debug_CheckWitness);
            Register("System.Blockchain.GetBlock", Debug_GetBlock);
            Register("System.Blockchain.GetTransactionFromBlock", Debug_GetTransactionFromBlock);

            DebugApplicationEngine.debugServices = debugServices;

            void Register(string name, ServiceMethod method)
            {
                var hash = BitConverter.ToUInt32(Encoding.ASCII.GetBytes(name).Sha256(), 0);
                debugServices.Add(hash, method);
            }
        }

        public event EventHandler<(UInt160 scriptHash, string eventName, NeoArray state)>? DebugNotify;
        public event EventHandler<(UInt160 scriptHash, string message)>? DebugLog;
        private readonly Func<byte[], bool> witnessChecker;
        private readonly IReadOnlyDictionary<uint, UInt256> blockHashMap;
        private readonly EvaluationStackAdapter resultStackAdapter;
        private readonly InvocationStackAdapter invocationStackAdapter;

        // TODO: Use TestModeGas constant when https://github.com/neo-project/neo/pull/2084 is merged
        public DebugApplicationEngine(IVerifiable container, StoreView storeView, Func<byte[], bool>? witnessChecker) : base(TriggerType.Application, container, storeView, 20_00000000)
        {
            this.witnessChecker = witnessChecker ?? CheckWitness;
            this.blockHashMap = storeView.Blocks.Find()
                .ToDictionary(
                    t => t.Value.Index,
                    t => t.Value.Hash == t.Key ? t.Key : throw new Exception("invalid hash"));

            Log += OnLog;
            Notify += OnNotify;
            resultStackAdapter = new EvaluationStackAdapter(this.ResultStack);
            invocationStackAdapter = new InvocationStackAdapter(this);
        }

        public override void Dispose()
        {
            Log -= OnLog;
            Notify -= OnNotify;
            base.Dispose();
        }

        private void OnNotify(object? sender, NotifyEventArgs args)
        {
            if (ReferenceEquals(sender, this))
            {
                DebugNotify?.Invoke(sender, (args.ScriptHash, args.EventName, args.State));
            }
        }

        private void OnLog(object? sender, LogEventArgs args)
        {
            if (ReferenceEquals(sender, this))
            {
                DebugLog?.Invoke(sender, (args.ScriptHash, args.Message));
            }
        }

        public bool ExecuteNextInstruction()
        {
            ExecuteNext();
            return true;
        }

        public bool ExecutePrevInstruction() => throw new NotSupportedException();

        public bool CatchBlockOnStack()
        {
            foreach (var executionContext in InvocationStack)
            {
                if (executionContext.TryStack?.Any(c => c.HasCatch) == true)
                {
                    return true;
                }
            }

            return false;
        }

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
            var hashOrPubkey = (byte[])engine.Convert(engine.Pop(), paramDescriptors[0]);
            return engine.witnessChecker(hashOrPubkey);
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

        public bool TryGetContract(UInt160 scriptHash, [MaybeNullWhen(false)] out Script script)
        {
            var contractState = Snapshot.Contracts.TryGet(scriptHash);
            if (contractState != null)
            {
                script = contractState.Script;
                return true;
            }

            script = default;
            return false;
        }

        public StorageContainerBase GetStorageContainer(UInt160 scriptHash)
            => new StorageContainer(scriptHash, Snapshot);

        IReadOnlyCollection<IExecutionContext> IApplicationEngine.InvocationStack => invocationStackAdapter;

        IReadOnlyList<StackItem> IApplicationEngine.ResultStack => resultStackAdapter;

        IExecutionContext? IApplicationEngine.CurrentContext => CurrentContext == null
            ? null
            : new ExecutionContextAdapter(CurrentContext);
    }
}
