using System;
using System.Collections.Generic;
using Neo.SmartContract;
using StackItem = Neo.VM.Types.StackItem;
using NeoArray = Neo.VM.Types.Array;
using Neo;
using Neo.VM;
using System.Diagnostics.CodeAnalysis;

namespace NeoDebug.Neo3
{
    internal interface IApplicationEngine : IDisposable
    {
        event EventHandler<(UInt160 scriptHash, string scriptName, string eventName, NeoArray state)>? DebugNotify;
        event EventHandler<(UInt160 scriptHash, string scriptName, string message)>? DebugLog;

        bool CatchBlockOnStack();

        bool ExecuteNextInstruction();
        bool ExecutePrevInstruction();
        bool TryGetContract(UInt160 scriptHash, [MaybeNullWhen(false)] out Script script);
        StorageContainerBase GetStorageContainer(UInt160 scriptHash);

        bool SupportsStepBack { get; }
        byte AddressVersion { get; }
        IReadOnlyCollection<IExecutionContext> InvocationStack { get; }
        IExecutionContext? CurrentContext { get; }
        IReadOnlyList<StackItem> ResultStack { get; }
        long GasConsumed { get; }
        Exception? FaultException { get; }
        VMState State { get; }
        bool AtStart { get; }
    }
}
