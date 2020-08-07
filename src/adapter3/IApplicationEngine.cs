using System;
using System.Collections.Generic;
using Neo.SmartContract;
using StackItem = Neo.VM.Types.StackItem;
using Neo;
using Neo.VM;
using System.Diagnostics.CodeAnalysis;

namespace NeoDebug.Neo3
{
    internal interface IApplicationEngine
    {
        event EventHandler<NotifyEventArgs> DebugNotify;
        event EventHandler<LogEventArgs> DebugLog;

        bool CatchBlockOnStack();
        void Dispose();
        bool ExecuteInstruction();
        bool TryGetContract(UInt160 scriptHash, [MaybeNullWhen(false)] out Script script);
        IStorageContainer GetStorageContainer(UInt160 scriptHash);

        IReadOnlyCollection<IExecutionContext> InvocationStack { get; }
        IExecutionContext? CurrentContext { get; }
        IReadOnlyList<StackItem> ResultStack { get; }
        StackItem UncaughtException { get; }
        UInt160 CurrentScriptHash { get; }
        VMState State { get; }
    }
}
