using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo.VM;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

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

        const VMState HAULT_OR_FAULT = VMState.HALT | VMState.FAULT;

        void Run(SequencePoint sequencePoint, bool stepIn = false)
        {
            var contractScriptHashSpan = Contract.ScriptHash.AsSpan();
            var currentStackDepth = engine.InvocationStack.Count;

            while ((engine.State & HAULT_OR_FAULT) == 0)
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

        public IEnumerable<Scope> GetScopes(int frameId)
        {
            if ((engine.State & HAULT_OR_FAULT) == 0)
            {
                var context = engine.InvocationStack.Peek(frameId);
                var method = Contract.GetMethod(context);

                if (method != null && engine.CurrentContext.AltStack.Peek(0) is Neo.VM.Types.Array alt)
                {
                    yield return new Scope(method.DisplayName, context.GetHashCode(), false)
                    {
                        PresentationHint = Scope.PresentationHintValue.Arguments,
                        NamedVariables = alt.Count,
                    };
                }
            }
        }

        public IEnumerable<Variable> GetVariables(int variableReference)
        {
            if ((engine.State & HAULT_OR_FAULT) == 0)
            {
                for (int i = 0; i < engine.InvocationStack.Count; i++)
                {
                    var context = engine.InvocationStack.Peek(i);
                    if (context.GetHashCode() == variableReference)
                    {
                        var method = Contract.GetMethod(context);

                        if (method != null && engine.CurrentContext.AltStack.Peek(0) is Neo.VM.Types.Array alt)
                        {
                            for (int j = 0; j < method.Parameters.Count; j++)
                            {
                                var p = method.Parameters[j];
                                var value = alt[j].GetStackItemValue(p.Type);
                                yield return new Variable(p.Name, value, 0)
                                {
                                    Type = p.Type
                                };
                            }

                            for (int j = method.Parameters.Count; j < alt.Count; j++)
                            {
                                var value = alt[j].GetStackItemValue();
                                yield return new Variable($"<variable {j}>", value, 0)
                                {
                                };
                            }
                        }
                    }
                }
            }
        }

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
    }
}
