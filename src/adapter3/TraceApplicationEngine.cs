using System;
using System.Collections.Generic;
using Neo.SmartContract;
using StackItem = Neo.VM.Types.StackItem;
using Neo;
using Neo.VM;
using System.Diagnostics.CodeAnalysis;

namespace NeoDebug.Neo3
{
    internal class TraceApplicationEngine : IApplicationEngine
    {
        public TraceApplicationEngine(string traceFilePath)
        {

        }
        
        public IReadOnlyCollection<IExecutionContext> InvocationStack => throw new NotImplementedException();

        public IExecutionContext? CurrentContext => throw new NotImplementedException();

        public IReadOnlyList<StackItem> ResultStack => throw new NotImplementedException();

        public StackItem UncaughtException => throw new NotImplementedException();

        public UInt160 CurrentScriptHash => throw new NotImplementedException();

        public VMState State => throw new NotImplementedException();

        public event EventHandler<NotifyEventArgs> DebugNotify;
        public event EventHandler<LogEventArgs> DebugLog;

        public bool CatchBlockOnStack()
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public bool ExecuteInstruction()
        {
            throw new NotImplementedException();
        }

        public IStorageContainer GetStorageContainer(UInt160 scriptHash)
        {
            throw new NotImplementedException();
        }

        public bool TryGetContract(UInt160 scriptHash, [MaybeNullWhen(false)] out Script script)
        {
            throw new NotImplementedException();
        }
    }
}
