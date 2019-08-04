using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo.VM;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Neo.DebugAdapter
{
    internal class NeoDebugSession
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

        public readonly Contract Contract;
        public readonly ContractArgument[] Arguments;
        private readonly ScriptTable ScriptTable = new ScriptTable();
        private readonly EmulatedInteropService InteropService = new EmulatedInteropService();
        private readonly DebugExecutionEngine engine;

        public VMState EngineState => engine.State;

        public IEnumerable<StackItem> GetResults() => engine.ResultStack;

        public NeoDebugSession(Contract contract, IEnumerable<ContractArgument> arguments)
        {
            Contract = contract;
            Arguments = arguments.ToArray();
            ScriptTable.Add(Contract);
            var builder = contract.BuildInvokeScript(Arguments);
            engine = new DebugExecutionEngine(null, new Crypto(), ScriptTable, InteropService);
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

        const VMState HALT_OR_FAULT = VMState.HALT | VMState.FAULT;

        void Run(SequencePoint sequencePoint, bool stepIn = false)
        {
            var contractScriptHashSpan = Contract.ScriptHash.AsSpan();
            var currentStackDepth = engine.InvocationStack.Count;

            while ((engine.State & HALT_OR_FAULT) == 0)
            {
                if (sequencePoint != null &&
                    contractScriptHashSpan.SequenceEqual(engine.CurrentContext.ScriptHash) &&
                    engine.CurrentContext.InstructionPointer == sequencePoint.Address)
                {
                    break;
                }

                if (stepIn && engine.InvocationStack.Count > currentStackDepth)
                {
                    var method = Contract.GetMethod(engine.CurrentContext);
                    var sp = method.GetNextSequencePoint(engine.CurrentContext);
                    if (sp != null)
                    {
                        Run(sp, true);
                        break;
                    }
                }

                engine.ExecuteNext();
            }

        }

        public void Continue()
        {
            Run(null);
        }

        public void StepOver()
        {
            var method = Contract.GetMethod(engine.CurrentContext);
            var sp = method.GetNextSequencePoint(engine.CurrentContext);

            Run(sp);
        }

        public void StepIn()
        {
            var method = Contract.GetMethod(engine.CurrentContext);
            var sp = method.GetNextSequencePoint(engine.CurrentContext);

            Run(sp, true);
        }

        public void StepOut()
        {
            //engine.State &= ~VMState.BREAK;
            //int stackCount = engine.InvocationStack.Count;

            //while (((engine.State & HAULT_FAULT_BREAK) == 0) && engine.InvocationStack.Count >= stackCount)
            //{
            //    ExecuteOne();
            //}

            //engine.State |= VMState.BREAK;
        }


        public IEnumerable<Thread> GetThreads()
        {
            yield return new Thread(1, "main thread");
        }

        public IEnumerable<StackFrame> GetStackFrames(StackTraceArguments args)
        {
            System.Diagnostics.Debug.Assert(args.ThreadId == 1);

            if ((engine.State & HALT_OR_FAULT) == 0)
            {
                var start = args.StartFrame ?? 0;
                var count = args.Levels ?? int.MaxValue;
                var end = Math.Min(engine.InvocationStack.Count, start + count);

                for (var i = start; i < end; i++)
                {
                    var context = engine.InvocationStack.Peek(i);

                    var frame = new StackFrame()
                    {
                        Id = i,
                        Name = $"unnamed frame",
                        ModuleId = context.ScriptHash,
                    };

                    var method = Contract.GetMethod(context);

                    if (method != null)
                    {
                        frame.Name = method.DisplayName;
                        SequencePoint sequencePoint = method.GetCurrentSequencePoint(context);

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

                    yield return frame;
                }
            }
        }

        private readonly Dictionary<int, IVariableContainer> variableContainers =
            new Dictionary<int, IVariableContainer>();

        public void ClearVariableContainers()
        {
            variableContainers.Clear();
        }

        public int AddVariableContainer(IVariableContainer container)
        {
            var id = container.GetHashCode();
            variableContainers.Add(id, container);
            return id;
        }

        public IEnumerable<Scope> GetScopes(ScopesArguments args)
        {
            if ((engine.State & HALT_OR_FAULT) == 0)
            {
                var context = engine.InvocationStack.Peek(args.FrameId);
                var containerID = AddVariableContainer(
                    new ExecutionContextContainer(this, context));
                yield return new Scope("Locals", containerID, false);
            }
        }

        public IEnumerable<Variable> GetVariables(VariablesArguments args)
        {
            if ((engine.State & HALT_OR_FAULT) == 0)
            {
                if (variableContainers.TryGetValue(args.VariablesReference, out var container))
                {
                    return container.GetVariables(args);
                }
            }

            return Enumerable.Empty<Variable>();
        }
    }
}
