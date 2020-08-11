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
using Neo.Ledger;

namespace NeoDebug.Neo3
{
    internal sealed partial class TraceApplicationEngine : IApplicationEngine
    {
        private bool disposedValue;
        private readonly TraceFile traceFile;
        private readonly Dictionary<UInt160, Script> contracts;
        private readonly Dictionary<UInt160, IReadOnlyList<(ReadOnlyMemory<byte>, StorageItem)>> storageMap = new Dictionary<UInt160, IReadOnlyList<(ReadOnlyMemory<byte>, StorageItem)>>();
        private TraceRecord? currentTraceRecord;

        public TraceApplicationEngine(string traceFilePath, IEnumerable<NefFile> contracts)
        {
            this.contracts = contracts.ToDictionary(c => c.ScriptHash, c => (Script)c.Script);
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
                    InvocationStack = trace.StackFrames
                        .Select(sf => new ExecutionContextAdapter(sf, contracts))
                        .ToList();
                    break;
                case StorageRecord storage:
                    storageMap[storage.ScriptHash] = storage.Storages
                        .Select(kvp => ((ReadOnlyMemory<byte>)kvp.Key, kvp.Value))
                        .ToList();
                    break;
                case NotifyRecord notify:
                    if (!stepBack)
                    {
                        DebugNotify?.Invoke(this, (notify.ScriptHash, notify.EventName, new NeoArray(notify.State)));
                    }
                    break;
                case LogRecord log:
                    if (!stepBack)
                    {
                        DebugLog?.Invoke(this, (log.ScriptHash, log.Message));
                    }
                    break;
                case ResultsRecord results:
                    ResultStack = stepBack ? new List<StackItem>() : results.ResultStack;
                    break;
                case FaultRecord fault:
                    // UncaughtException = stepForward ? true : StackItem.Null;
                    break;
                default:
                    throw new InvalidDataException($"TraceDebugRecord {record.GetType().Name}");
            }
        }

        public VMState State { get; private set; }
        public IReadOnlyCollection<IExecutionContext> InvocationStack { get; private set; } = new List<IExecutionContext>();
        public IExecutionContext? CurrentContext => InvocationStack.FirstOrDefault();
        public IReadOnlyList<StackItem> ResultStack { get; private set; } = new List<StackItem>();
        public StackItem? UncaughtException { get; private set; }
        public event EventHandler<(UInt160 scriptHash, string eventName, NeoArray state)>? DebugNotify;
        public event EventHandler<(UInt160 scriptHash, string message)>? DebugLog;

        public bool CatchBlockOnStack()
        {
            return false;
        }

        public bool TryGetContract(UInt160 scriptHash, [MaybeNullWhen(false)] out Script script)
        {
            return contracts.TryGetValue(scriptHash, out script);
        }

        public StorageContainer GetStorageContainer(UInt160 scriptHash)
        {
            return new TraceStorageContainer(
                storageMap.TryGetValue(scriptHash, out var storages)
                    ? storages
                    : Enumerable.Empty<(ReadOnlyMemory<byte>, StorageItem)>());
        }
    }
}
