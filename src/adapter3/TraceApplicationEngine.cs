using System;
using System.Collections.Generic;
using Neo.SmartContract;
using StackItem = Neo.VM.Types.StackItem;
using NeoArray = Neo.VM.Types.Array;
using Neo;
using Neo.VM;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using MessagePack;
using MessagePack.Resolvers;
using System.Buffers;
using Neo.BlockchainToolkit.TraceDebug;
using System.Linq;
using System.Collections.Immutable;

namespace NeoDebug.Neo3
{
    internal partial class TraceApplicationEngine : IApplicationEngine
    {
        private readonly MessagePackSerializerOptions options = MessagePackSerializerOptions.Standard
            .WithResolver(TraceDebugResolver.Instance);
        private readonly Stream traceFileStream;
        private readonly Dictionary<UInt160, Script> contracts;

        private readonly Stack<ITraceDebugRecord> previousRecords = new Stack<ITraceDebugRecord>();
        private readonly Stack<ITraceDebugRecord> nextRecords = new Stack<ITraceDebugRecord>();

        public event EventHandler<(UInt160 scriptHash, string eventName, NeoArray state)>? DebugNotify;
        public event EventHandler<(UInt160 scriptHash, string message)>? DebugLog;

        public TraceApplicationEngine(string traceFilePath, IEnumerable<NefFile> contracts)
        {
            traceFileStream = File.OpenRead(traceFilePath);

            this.contracts = contracts.ToDictionary(c => c.ScriptHash, c => (Script)c.Script);
            ExecuteNextInstruction();
        }

        public void Dispose()
        {
            traceFileStream.Dispose();
        }

        public IReadOnlyCollection<IExecutionContext> InvocationStack { get; private set; } = new List<IExecutionContext>();

        public IExecutionContext? CurrentContext => InvocationStack.FirstOrDefault();

        public IReadOnlyList<StackItem> ResultStack { get; private set; } = new List<StackItem>();

        public StackItem? UncaughtException { get; private set; }

        public UInt160 CurrentScriptHash => CurrentContext?.ScriptHash ?? UInt160.Zero;

        public VMState State { get; private set; }

        public bool CatchBlockOnStack()
        {
            return false;
        }

        private void ProcessRecord(ITraceDebugRecord record, bool stepForward)
        {
            switch (record)
            {
                case TraceRecord trace:
                    State = trace.State;
                    InvocationStack = trace.StackFrames
                        .Select(sf => new ExecutionContextAdapter(sf, contracts))
                        .ToList();
                    break;
                case NotifyRecord notify:
                    if (stepForward)
                    {
                        DebugNotify?.Invoke(this, (notify.ScriptHash, notify.EventName, new NeoArray(notify.State)));
                    }
                    break;
                case LogRecord log:
                    if (stepForward)
                    {
                        DebugLog?.Invoke(this, (log.ScriptHash, log.Message));
                    }
                    break;
                case ResultsRecord results:
                    ResultStack = stepForward ? results.ResultStack : new List<StackItem>();
                    break;
                case FaultRecord fault:
                    UncaughtException = stepForward ? true : StackItem.Null;
                    break;
                default:
                    throw new InvalidDataException($"TraceDebugRecord {record.GetType().Name}");
            }
        }


        public bool ExecuteNextInstruction()
        {
            while (TryGetNextRecord(out var record))
            {
                if (record is ScriptRecord script)
                {
                    contracts.Add(script.ScriptHash, script.Script);
                }
                else
                {
                    ProcessRecord(record, false);
                    previousRecords.Push(record);
                }

                if (record is TraceRecord)
                {
                    break;
                }
            }

            return false;

            bool TryGetNextRecord(out ITraceDebugRecord record)
            {
                if (nextRecords.TryPop(out record!))
                {
                    return true;
                }

                if (traceFileStream.Position < traceFileStream.Length)
                {
                    record = MessagePackSerializer.Deserialize<ITraceDebugRecord>(traceFileStream, options);
                    return true;
                }

                return false;
            }
        }

        public bool ExecutePrevInstruction()
        {
            while (previousRecords.Count > 0)
            {
                var record = previousRecords.Pop();
                if (record is TraceRecord trace)
                {
                    ProcessRecord(trace, true);
                    break;
                }
                nextRecords.Push(record);
            }

            return false;
        }

        public IStorageContainer GetStorageContainer(UInt160 scriptHash)
        {
            return new TraceStorageContainer();
        }

        public bool TryGetContract(UInt160 scriptHash, [MaybeNullWhen(false)] out Script script)
            => contracts.TryGetValue(scriptHash, out script);
    }
}
