using Neo.VM;
using NeoDebug.Models;
using NeoDebug.VariableContainers;
using System.Collections.Generic;

namespace NeoDebug.Adapter
{
    internal class DebugExecutionEngine : ExecutionEngine, IExecutionEngine
    {
        private readonly ScriptTable scriptTable;
        private readonly EmulatedStorage storage;

        private DebugExecutionEngine(ScriptTable scriptTable, EmulatedStorage storage, EmulatedRuntime runtime)
            : base(null, new Crypto(), scriptTable, new EmulatedInteropService(storage, runtime))
        {
            this.scriptTable = scriptTable;
            this.storage = storage;
        }

        public static DebugExecutionEngine Create(Contract contract, EmulatedStorage storage, EmulatedRuntime runtime)
        {
            var table = new ScriptTable();
            table.Add(contract);
            
            return new DebugExecutionEngine(table, storage, runtime);
        }

        VMState IExecutionEngine.State { get => State; set { State = value; } }

        IEnumerable<StackItem> IExecutionEngine.ResultStack => ResultStack;

        ExecutionContext IExecutionEngine.CurrentContext => CurrentContext;

        RandomAccessStack<ExecutionContext> IExecutionEngine.InvocationStack => InvocationStack;

        ExecutionContext IExecutionEngine.LoadScript(byte[] script, int rvcount) => LoadScript(script, rvcount);

        void IExecutionEngine.ExecuteNext() => ExecuteNext();

        IVariableContainer IExecutionEngine.GetStorageContainer(IVariableContainerSession session) => storage.GetStorageContainer(session);
    }
}
