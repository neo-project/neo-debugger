using System;
using System.Collections.Generic;
using Neo.SmartContract;
using StackItem = Neo.VM.Types.StackItem;
using NeoArray = Neo.VM.Types.Array;
using Neo;
using Neo.VM;
using System.Diagnostics.CodeAnalysis;
using Neo.BlockchainToolkit.TraceDebug;
using System.Linq;
using System.IO;
using Neo.SmartContract.Native;

namespace NeoDebug.Neo3
{
    internal sealed partial class TraceApplicationEngine : IApplicationEngine
    {
        private bool disposedValue;
        private readonly TraceFile traceFile;
        private readonly Dictionary<UInt160, Script> contracts;
        private TraceRecord? currentTraceRecord;
        private IEnumerable<TraceRecord.StackFrame> stackFrames = Enumerable.Empty<TraceRecord.StackFrame>();

        public TraceApplicationEngine(string traceFilePath, IEnumerable<NefFile> contracts)
        {
            this.contracts = contracts.ToDictionary(c => Neo.SmartContract.Helper.ToScriptHash(c.Script.Span), c => (Script)c.Script);
            traceFile = new TraceFile(traceFilePath, this.contracts);

            while (traceFile.TryGetNext(out var record))
            {
                ProcessRecord(record);
                if (record is TraceRecord trace)
                {
                    currentTraceRecord = trace;
                    break;
                }
            }
        }

        public void Dispose()
        {
            if (!disposedValue)
            {
                traceFile.Dispose();
                disposedValue = true;
            }
        }

        bool IApplicationEngine.SupportsStepBack => true;

        public bool ExecuteNextInstruction()
        {
            while (traceFile.TryGetNext(out var record))
            {
                ProcessRecord(record);
                if (record is TraceRecord trace
                    && !ReferenceEquals(trace, currentTraceRecord))
                {
                    currentTraceRecord = trace;
                    return true;
                }
            }

            return false;
        }

        public bool ExecutePrevInstruction()
        {
            while (traceFile.TryGetPrev(out var record))
            {
                ProcessRecord(record, true);
                if (record is TraceRecord trace
                    && !ReferenceEquals(trace, currentTraceRecord))
                {
                    currentTraceRecord = trace;
                    return true;
                }
            }

            return false;
        }

        private void ProcessRecord(ITraceDebugRecord record, bool stepBack = false)
        {
            switch (record)
            {
                case TraceRecord trace:
                    State = trace.State;
                    GasConsumed = trace.GasConsumed;
                    stackFrames = trace.StackFrames;
                    InvocationStack = trace.StackFrames
                        .Select(sf => new ExecutionContextAdapter(sf, contracts))
                        .ToList();
                    break;
                case StorageRecord _:
                    break;
                case NotifyRecord notify:
                    if (!stepBack)
                    {
                        DebugNotify?.Invoke(this, (notify.ScriptHash, notify.ScriptName, notify.EventName, new NeoArray(notify.State)));
                    }
                    break;
                case LogRecord log:
                    if (!stepBack)
                    {
                        DebugLog?.Invoke(this, (log.ScriptHash, log.ScriptName, log.Message));
                    }
                    break;
                case ResultsRecord results:
                    ResultStack = stepBack ? new List<StackItem>() : results.ResultStack;
                    break;
                case FaultRecord fault:
                    FaultException = stepBack ? null : new Exception(fault.Exception);
                    break;
                default:
                    throw new InvalidDataException($"TraceDebugRecord {record.GetType().Name}");
            }
        }

        public bool AtStart => traceFile.AtStart;
        public byte AddressVersion => traceFile.AddressVersion;
        public VMState State { get; private set; }
        public IReadOnlyCollection<IExecutionContext> InvocationStack { get; private set; } = new List<IExecutionContext>();
        public IExecutionContext? CurrentContext => InvocationStack.FirstOrDefault();
        public IReadOnlyList<StackItem> ResultStack { get; private set; } = new List<StackItem>();
        public Exception? FaultException { get; private set; }
        public long GasConsumed { get; private set; }

        public event EventHandler<(UInt160 scriptHash, string scriptName, string eventName, NeoArray state)>? DebugNotify;
        public event EventHandler<(UInt160 scriptHash, string scriptName, string message)>? DebugLog;

        public bool CatchBlockOnStack() => stackFrames.Any(f => f.HasCatch);

        public bool TryGetContract(UInt160 scriptHash, [MaybeNullWhen(false)] out Script script)
        {
            return contracts.TryGetValue(scriptHash, out script);
        }

        public StorageContainerBase GetStorageContainer(UInt160 scriptHash)
        {
            return new StorageContainer(traceFile.FindStorage(scriptHash));
        }
    }
}
