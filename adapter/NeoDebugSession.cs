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

        public IEnumerable<StackItem> GetResults() => engine.ResultStack;

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
                var start = args.StartFrame.HasValue ? args.StartFrame.Value : 0;
                var count = args.Levels.HasValue
                    ? Math.Min(engine.InvocationStack.Count, start + args.Levels.Value)
                    : engine.InvocationStack.Count;
                    
                for (var i = start; i < count; i++)
                {
                    var ctx = engine.InvocationStack.Peek(i);

                    var frame = new StackFrame()
                    {
                        Id = i,
                        Name = $"unnamed frame",
                        ModuleId = ctx.ScriptHash,
                    };

                    var method = Contract.GetMethod(ctx);

                    if (method != null)
                    {
                        frame.Name = method.DisplayName;
                        SequencePoint sequencePoint = method.GetCurrentSequencePoint(ctx);

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

        interface IVariableContainer
        {
            IEnumerable<Variable> GetVariables(VariablesArguments args);
        }

        class ExecutionContextContainer : IVariableContainer
        {
            private readonly ExecutionContext context;
            private readonly Method method;

            public ExecutionContextContainer(ExecutionContext context, Method method)
            {
                this.context = context;
                this.method = method;
            }
            
            public IEnumerable<Variable> GetVariables(VariablesArguments args)
            {
                if (method != null && context.AltStack.Peek(0) is Neo.VM.Types.Array alt)
                {
                    for (int j = 0; j < method.Parameters.Count; j++)
                    {
                        var p = method.Parameters[j];
                        var value = alt[j].GetStackItemValue(p.Type);
                        yield return new Variable(p.Name, value, 0);
                    }

                    for (int j = method.Parameters.Count; j < alt.Count; j++)
                    {
                        var value = alt[j].GetStackItemValue();
                        yield return new Variable($"<variable {j}>", value, 0);
                    }
                }
            }
        }

        private readonly Dictionary<int, IVariableContainer> variableContainers =
            new Dictionary<int, IVariableContainer>();

        public void ClearVariableContainers()
        {
            variableContainers.Clear();
        }

        public IEnumerable<Scope> GetScopes(ScopesArguments args)
        {
            if ((engine.State & HALT_OR_FAULT) == 0)
            {
                var context = engine.InvocationStack.Peek(args.FrameId);
                var method = Contract.GetMethod(context);

                if (method != null && engine.CurrentContext.AltStack.Peek(0) is Neo.VM.Types.Array alt)
                {
                    variableContainers.Add(context.GetHashCode(), new ExecutionContextContainer(context, method));
                    yield return new Scope(method.DisplayName, context.GetHashCode(), false)
                    {
                        PresentationHint = Scope.PresentationHintValue.Arguments,
                        NamedVariables = alt.Count,
                    };
                }
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
