using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Neo;
using Neo.BlockchainToolkit.SmartContract;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.VM;

using NeoArray = Neo.VM.Types.Array;
using StackItem = Neo.VM.Types.StackItem;

namespace NeoDebug.Neo3
{
    internal partial class DebugApplicationEngine : TestApplicationEngine, IApplicationEngine
    {
        private readonly EvaluationStackAdapter resultStackAdapter;
        private readonly InvocationStackAdapter invocationStackAdapter;
        private readonly IDictionary<UInt160, UInt160> scriptIdMap = new Dictionary<UInt160, UInt160>();

        public event EventHandler<(UInt160 scriptHash, string scriptName, string eventName, NeoArray state)>? DebugNotify;
        public event EventHandler<(UInt160 scriptHash, string scriptName, string message)>? DebugLog;

        public DebugApplicationEngine(IVerifiable container, DataCache snapshot, Block persistingBlock, ProtocolSettings settings, Func<byte[], bool>? witnessChecker)
            : base(TriggerType.Application, container, snapshot, persistingBlock, settings, TestModeGas, witnessChecker)
        {
            this.Log += OnLog;
            this.Notify += OnNotify;
            resultStackAdapter = new EvaluationStackAdapter(this.ResultStack);
            invocationStackAdapter = new InvocationStackAdapter(this);
        }

        public override void Dispose()
        {
            this.Log -= OnLog;
            this.Notify -= OnNotify;
            base.Dispose();
        }

        private void OnNotify(object? sender, NotifyEventArgs args)
        {
            var name = GetContractName(args.ScriptHash);
            DebugNotify?.Invoke(sender, (args.ScriptHash, name, args.EventName, args.State));
        }

        private void OnLog(object? sender, LogEventArgs args)
        {
            var name = GetContractName(args.ScriptHash);
            DebugLog?.Invoke(sender, (args.ScriptHash, name, args.Message));
        }

        private string GetContractName(UInt160 scriptHash)
        {
            var contract = NativeContract.ContractManagement.GetContract(Snapshot, scriptHash);
            return contract?.Manifest?.Name ?? string.Empty;
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
