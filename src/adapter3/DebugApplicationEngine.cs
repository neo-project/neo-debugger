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

namespace NeoDebug.Neo3
{
    using ServiceMethod = Func<DebugApplicationEngine, IReadOnlyList<InteropParameterDescriptor>, StackItem?>;

    internal partial class DebugApplicationEngine : ApplicationEngine, IDebugApplicationEngine
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

        public event EventHandler<NotifyEventArgs>? DebugNotify;
        public event EventHandler<LogEventArgs>? DebugLog;
        private readonly WitnessChecker witnessChecker;
        private readonly IReadOnlyDictionary<uint, UInt256> blockHashMap;
        private readonly EvaluationStackAdapter resultStackAdapter;
        private readonly InvocationStackAdapter invocationStackAdapter;

        public DebugApplicationEngine(IVerifiable container, StoreView storeView, WitnessChecker witnessChecker) : base(TriggerType.Application, container, storeView, 0, true)
        {
            this.witnessChecker = witnessChecker;
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
                DebugNotify?.Invoke(sender, args);
            }
        }

        private void OnLog(object? sender, LogEventArgs args)
        {
            if (ReferenceEquals(sender, this))
            {
                DebugLog?.Invoke(sender, args);
            }
        }

        private int lastThrowAddress = -1;

        public bool ExecuteInstruction()
        {
            // ExecutionEngine does not provide a mechanism to halt execution
            // after an exception is thrown but before it has been handled.
            // The debugger needs to be able to treat the exception throw and
            // handling as separate operations. So DebugApplicationEngine inserts
            // a dummy instruction execution when it detects a THROW opcode.
            // The THROW operation address is used to ensure only a single dummy
            // instruction is executed. The ExecuteInstruction return value
            // indicates that a dummy THROW instruction has been inserted.

            if (CurrentContext.CurrentInstruction.OpCode == OpCode.THROW
                && CurrentContext.InstructionPointer != lastThrowAddress)
            {
                lastThrowAddress = CurrentContext.InstructionPointer;
                return true;
            }

            ExecuteNext();
            lastThrowAddress = -1;
            return false;
        }

        public bool CatchBlockOnStack()
        {
            foreach (var executionContext in InvocationStack)
            {
                foreach (var tryContext in executionContext.TryStack ?? Enumerable.Empty<ExceptionHandlingContext>())
                {
                    if (tryContext.HasCatch)
                    {
                        return true;
                    }
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
            _ = (byte[])engine.Convert(engine.Pop(), paramDescriptors[0]);

            return engine.witnessChecker.Check(Neo.UInt160.Zero);
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

        public IStorageContainer GetStorageContainer(UInt160 scriptHash)
            => new StorageContainer(scriptHash, Snapshot);

        IReadOnlyCollection<IExecutionContext> IDebugApplicationEngine.InvocationStack => invocationStackAdapter;

        IReadOnlyList<StackItem> IDebugApplicationEngine.ResultStack => resultStackAdapter;

        IExecutionContext IDebugApplicationEngine.CurrentContext => new ExecutionContextAdapter(CurrentContext);
    }
}
