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
using Neo.SmartContract.Native;

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

            DebugApplicationEngine.debugServices = debugServices;

            void Register(string name, ServiceMethod method)
            {
                var hash = BitConverter.ToUInt32(Encoding.ASCII.GetBytes(name).Sha256(), 0);
                debugServices.Add(hash, method);
            }
        }

        public event EventHandler<(UInt160 scriptHash, string scriptName, string eventName, NeoArray state)>? DebugNotify;
        public event EventHandler<(UInt160 scriptHash, string scriptName, string message)>? DebugLog;
        private readonly Func<byte[], bool> witnessChecker;
        private readonly EvaluationStackAdapter resultStackAdapter;
        private readonly InvocationStackAdapter invocationStackAdapter;
        private readonly IDictionary<UInt160, UInt160> scriptIdMap = new Dictionary<UInt160, UInt160>();

        public DebugApplicationEngine(IVerifiable container, DataCache snapshot, Block persistingBlock, Func<byte[], bool>? witnessChecker) : base(TriggerType.Application, container, snapshot, persistingBlock, TestModeGas)
        {
            this.witnessChecker = witnessChecker ?? CheckWitness;
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
            var contract = NativeContract.ContractManagement.GetContract(Snapshot, args.ScriptHash);
            var name = contract == null ? string.Empty : (contract.Manifest.Name ?? string.Empty);

            if (ReferenceEquals(sender, this))
            {
                DebugNotify?.Invoke(sender, (args.ScriptHash, name, args.EventName, args.State));
            }
        }

        private void OnLog(object? sender, LogEventArgs args)
        {
            var contract = NativeContract.ContractManagement.GetContract(Snapshot, args.ScriptHash);
            var name = contract == null ? string.Empty : (contract.Manifest.Name ?? string.Empty);

            if (ReferenceEquals(sender, this))
            {
                DebugLog?.Invoke(sender, (args.ScriptHash, name, args.Message));
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

        public bool TryGetContract(UInt160 scriptHash, [MaybeNullWhen(false)] out Script script)
        {
            var contractState = NativeContract.ContractManagement.GetContract(Snapshot, scriptHash);
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
            : new ExecutionContextAdapter(CurrentContext, scriptIdMap);
    }
}
