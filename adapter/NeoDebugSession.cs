using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo.VM;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Neo.DebugAdapter
{
    class NeoDebugSession
    {
        // Helper class to expose ExecutionEngine internals to NeoDebugSession
        class DebugExecutionEngine : ExecutionEngine
        {
            public DebugExecutionEngine(IScriptContainer container, ICrypto crypto, IScriptTable table = null, IInteropService service = null) : base(container, crypto, table, service)
            {
            }

            new public VMState State
            {
                get { return base.State; }
                set { base.State = value; }
            }

            new public void ExecuteNext()
            {
                base.ExecuteNext();
            }
        }

        public Contract Contract;
        public ContractArgument[] Arguments;
        public ScriptTable ScriptTable = new ScriptTable();
        readonly DebugExecutionEngine engine;

        public VMState EngineState => engine.State;

        public IEnumerable<StackItem> GetResults()
        {
            foreach (var item in engine.ResultStack)
            {
                yield return item.GetResult();
            }
        }

        public NeoDebugSession(Contract contract, IEnumerable<ContractArgument> arguments)
        {
            Contract = contract;
            Arguments = arguments.ToArray();
            ScriptTable.Add(Contract);
            var builder = contract.BuildInvokeScript(Arguments);
            engine = new DebugExecutionEngine(null, new Crypto(), ScriptTable);
            engine.LoadScript(builder.ToArray());
        }

        //private readonly Dictionary<int, HashSet<int>> breakPoints = new Dictionary<int, HashSet<int>>();

        //public void AddBreakPoint(byte[] scriptHash, int position)
        //{
        //    var key = Crypto.GetHashCode(scriptHash);
        //    if (!breakPoints.TryGetValue(key, out var hashset))
        //    {
        //        hashset = new HashSet<int>();
        //        breakPoints.Add(key, hashset);
        //    }
        //    hashset.Add(position);
        //}

        //public bool RemoveBreakPoint(byte[] scriptHash, int position)
        //{
        //    var key = Crypto.GetHashCode(scriptHash);
        //    if (breakPoints.TryGetValue(key, out var hashset))
        //    {
        //        return hashset.Remove(position);
        //    }
        //    return false;
        //}

        const VMState HAULT_OR_FAULT = VMState.HALT | VMState.FAULT;
        //const VMState HAULT_FAULT_BREAK = VMState.HALT | VMState.FAULT | VMState.BREAK;

        //void ExecuteOne()
        //{
        //    if ((engine.State & (VMState.HALT | VMState.FAULT)) != 0)
        //    {
        //        engine.ExecuteNext();
        //    }

        //    if (((engine.State & HAULT_FAULT_BREAK) == 0) && engine.InvocationStack.Count > 0 && breakPoints.Count > 0)
        //    {
        //        var key = Crypto.GetHashCode(engine.CurrentContext.ScriptHash);
        //        if (breakPoints.TryGetValue(key, out var hashset) && hashset.Contains(engine.CurrentContext.InstructionPointer))
        //        {
        //            engine.State |= VMState.BREAK;
        //        }
        //    }
        //}


        public void RunTo(byte[] scriptHash, int position)
        {
            var scriptHashSpan = scriptHash.AsSpan();
         
            while ((engine.State & HAULT_OR_FAULT) == 0)
            {
                if (scriptHashSpan.SequenceEqual(engine.CurrentContext.ScriptHash))
                {
                    if (engine.CurrentContext.InstructionPointer == position)
                        break;
                }

                engine.ExecuteNext();
            }
        }

        //public void Continue()
        //{
        //    engine.State &= ~VMState.BREAK;
        //    while ((engine.State & HAULT_FAULT_BREAK) == 0)
        //    {
        //        ExecuteOne();
        //    }
        //}

        //public void StepOver()
        //{
        //    if ((engine.State & HAULT_FAULT) == 0)
        //    {
        //        engine.State &= ~VMState.BREAK;
        //        int stackCount = engine.InvocationStack.Count;

        //        do
        //        {
        //            ExecuteOne();
        //        }
        //        while (((engine.State & HAULT_FAULT_BREAK) == 0) && engine.InvocationStack.Count > stackCount);

        //        engine.State |= VMState.BREAK;
        //    }
        //}

        //public void StepIn()
        //{
        //    if ((engine.State & HAULT_FAULT) == 0)
        //    {
        //        ExecuteOne();
        //        engine.State |= VMState.BREAK;
        //    }
        //}

        //public void StepOut()
        //{
        //    engine.State &= ~VMState.BREAK;
        //    int stackCount = engine.InvocationStack.Count;

        //    while (((engine.State & HAULT_FAULT_BREAK) == 0) && engine.InvocationStack.Count >= stackCount)
        //    {
        //        ExecuteOne();
        //    }

        //    engine.State |= VMState.BREAK;
        //}

        public IEnumerable<StackFrame> GetStackFrames()
        {
            if ((engine.State & HAULT_OR_FAULT) == 0)
            {
                for (var i = 0; i < engine.InvocationStack.Count; i++)
                {
                    var ctx = engine.InvocationStack.Peek(i);

                    var frame = new StackFrame()
                    {
                        Id = i,
                        Name = $"frame {i}",
                        ModuleId = ctx.ScriptHash,
                    };

                    if (ctx.ScriptHash.AsSpan().SequenceEqual(Contract.ScriptHash))
                    {
                        Method method = Contract.DebugInfo.Methods
                            .SingleOrDefault(m =>
                                m.StartAddress <= ctx.InstructionPointer
                                && m.EndAddress >= ctx.InstructionPointer);

                        if (method != null)
                        {
                            frame.Name = method.DisplayName;
                            SequencePoint sequencePoint = method.SequencePoints
                                .SingleOrDefault(sp => sp.Address == ctx.InstructionPointer);

                            if (sequencePoint != null)
                            {
                                frame.Source = new Source()
                                {
                                    Name = Path.GetFileName(sequencePoint.Document),
                                    Path = sequencePoint.Document
                                };
                                frame.Line = sequencePoint.StartLine;
                                frame.Column = sequencePoint.StartColumn;
                                frame.EndLine = sequencePoint.EndLine;
                                frame.EndColumn = sequencePoint.EndColumn;
                            }
                        }
                    }

                    yield return frame;
                }
            }
        }
    }
}
