using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol;
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

        const VMState HAULT_OR_FAULT = VMState.HALT | VMState.FAULT;

        public void RunTo(byte[] scriptHash, SequencePoint point)
        {
            var scriptHashSpan = scriptHash.AsSpan();
         
            while ((engine.State & HAULT_OR_FAULT) == 0)
            {
                if (scriptHashSpan.SequenceEqual(engine.CurrentContext.ScriptHash))
                {
                    if (engine.CurrentContext.InstructionPointer == point.Address)
                        break;
                }

                engine.ExecuteNext();
            }
        }

        public void Continue()
        {
            while ((engine.State & HAULT_OR_FAULT) == 0)
            {
                engine.ExecuteNext();
            }
        }

        public void StepOver()
        {
            // run to the next sequence point in the current method

            var method = Contract.GetMethod(engine.CurrentContext);
            var ip = engine.CurrentContext.InstructionPointer;
            var sp = method.SequencePoints.FirstOrDefault(p => p.Address > ip);

            if (sp != null)
            {
                RunTo(Contract.ScriptHash, sp);
            }
            else
            {
                Continue();
            }
        }

        public void StepIn()
        {

            //if ((engine.State & HAULT_FAULT) == 0)
            //{
            //    ExecuteOne();
            //    engine.State |= VMState.BREAK;
            //}
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

                if (method != null)
                {
                    yield return new Scope(method.DisplayName, context.GetHashCode(), false)
                    {
                        PresentationHint = Scope.PresentationHintValue.Arguments,
                        NamedVariables = method.GetParamCount(),
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

                        var alt = engine.CurrentContext.AltStack.Peek(0) as Neo.VM.Types.Array;
                        if (alt != null && alt.Count == method.GetParamCount())
                        {
                            for (int j = 0; j < method.Parameters.Count; j++)
                            {
                                var p = method.Parameters[j];
                                var value = alt[j].GetStackItemValue(p.Type);
                                yield return new Variable(p.Name, value, 0);
                            }

                            // TODO if retval is not void
                            var retValue = alt.Last().GetStackItemValue(method.ReturnType);
                            yield return new Variable("<return value>", retValue, 0);
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
                        Name = $"frame {i}",
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
