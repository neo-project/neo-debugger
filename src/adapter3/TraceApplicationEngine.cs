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
            ExecuteInstruction();
        }

        public void Dispose()
        {
            traceFileStream.Dispose();
        }

        public IReadOnlyCollection<IExecutionContext> InvocationStack { get; private set; }

        public IExecutionContext? CurrentContext => InvocationStack.FirstOrDefault();

        public IReadOnlyList<StackItem> ResultStack { get; private set; }

        public StackItem? UncaughtException { get; private set; }

        public UInt160 CurrentScriptHash => CurrentContext?.ScriptHash ?? UInt160.Zero;

        public VMState State { get; private set; }

        public bool CatchBlockOnStack()
        {
            return false;
        }

        private ITraceDebugRecord GetNextRecord()
        {
            if (nextRecords.TryPop(out var record))
            {
                return record;
            }

            if (traceFileStream.Position < traceFileStream.Length)
            {
                return MessagePackSerializer.Deserialize<ITraceDebugRecord>(traceFileStream, options);
            }

            throw new Exception("no more records");
        }

        public bool ExecuteInstruction()
        {
            while (traceFileStream.Position < traceFileStream.Length)
            {
                var record = MessagePackSerializer.Deserialize<ITraceDebugRecord>(traceFileStream, options);
                switch (record)
                {
                    case TraceRecord trace:
                        State = trace.State;
                        InvocationStack = trace.StackFrames
                            .Select(sf => new ExecutionContextAdapter(sf, contracts))
                            .ToList();
                        return false;
                    case NotifyRecord notify:
                        DebugNotify?.Invoke(this, (notify.ScriptHash, notify.EventName, new NeoArray(notify.State)));
                        break;
                    case LogRecord log:
                        DebugLog?.Invoke(this, (log.ScriptHash, log.Message));
                        break;
                    case ResultsRecord results:
                        ResultStack = results.ResultStack;
                        break;
                    case FaultRecord fault:
                        // UncaughtException = new Neo.VM.Types.ByteString(fault.
                        break;
                    case ScriptRecord script:
                        contracts.Add(script.ScriptHash, script.Script);
                        break;
                    default:
                        throw new Exception("unknown TraceDebugRecord");
                }
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
