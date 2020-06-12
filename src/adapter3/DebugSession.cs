using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo;
using Neo.VM;
using NeoDebug;

namespace NeoDebug.Neo3
{
    class DebugSession : IDebugSession, IDisposable
    {
        const VMState HALT_OR_FAULT = VMState.HALT | VMState.FAULT;

        private readonly DebugApplicationEngine engine;
        private readonly Action<DebugEvent> sendEvent;
        private bool disassemblyView;
        private readonly DisassemblyManager disassemblyManager;
        private readonly VariableManager variableManager = new VariableManager();
        private readonly BreakpointManager breakpointManager;
        private readonly IReadOnlyDictionary<UInt160, DebugInfo> debugInfoMap;

        public DebugSession(DebugApplicationEngine engine, Action<DebugEvent> sendEvent, IEnumerable<DebugInfo> debugInfos, DebugView defaultDebugView)
        {
            this.engine = engine;
            this.sendEvent = sendEvent;
            this.disassemblyView = defaultDebugView == DebugView.Disassembly;
            this.disassemblyManager = new DisassemblyManager(TryGetScript, TryGetDebugInfo);
            this.breakpointManager = new BreakpointManager(this.disassemblyManager, debugInfos);
            this.debugInfoMap = debugInfos.ToImmutableDictionary(d => d.ScriptHash);
        }

        public void Dispose()
        {
            engine.Dispose();
        }

        bool TryGetScript(Neo.UInt160 scriptHash, [MaybeNullWhen(false)] out Neo.VM.Script script)
        {
            var contractState = engine.Snapshot.Contracts.TryGet(scriptHash);
            if (contractState != null)
            {
                script = contractState.Script;
                return true;
            }

            foreach (var context in engine.InvocationStack)
            {
                if (scriptHash == Neo.SmartContract.Helper.ToScriptHash(context.Script))
                {
                    script = context.Script;
                    return true;
                }
            }

            script = null;
            return false;
        }

        bool TryGetDebugInfo(Neo.UInt160 scriptHash, [MaybeNullWhen(false)] out DebugInfo debugInfo)
        {
            return debugInfoMap.TryGetValue(scriptHash, out debugInfo);
        }

        public void Start()
        {
            if (disassemblyView)
            {
                FireStoppedEvent(StoppedEvent.ReasonValue.Entry);
            }
            else
            {
                StepIn();
            }
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
                foreach (var (context, index) in engine.InvocationStack.Select((c, i) => (c, i)))
                {
                    // TODO: ExecutionContext needs a mechanism to retrieve script hash 
                    //       https://github.com/neo-project/neo/issues/1696
                    var scriptHash = Neo.SmartContract.Helper.ToScriptHash(context.Script);
                    DebugInfo.Method? method = null;
                    if (debugInfoMap.TryGetValue(scriptHash, out var debugInfo))
                    {
                        method = debugInfo.GetMethod(context.InstructionPointer);
                    }

                    var frame = new StackFrame()
                    {
                        Id = index,
                        Name = method?.Name ?? $"frame {engine.InvocationStack.Count - index}",
                    };

                    if (disassemblyView)
                    {
                        var disassembly = disassemblyManager.GetDisassembly(context.Script, debugInfo);
                        var line = disassembly.AddressMap[context.InstructionPointer];
                        frame.Source = new Source()
                        {
                            SourceReference = disassembly.SourceReference,
                            Name = disassembly.Name,
                            Path = disassembly.Name,
                        };
                        frame.Line = index == 0 ? line : line - 1;
   
                    }
                    else
                    {
                        var sequencePoint = method.GetCurrentSequencePoint(context.InstructionPointer);

                        if (sequencePoint != null)
                        {
                            frame.Source = new Source()
                            {
                                Name = System.IO.Path.GetFileName(sequencePoint.Document),
                                Path = sequencePoint.Document
                            };
                            frame.Line = sequencePoint.Start.line;
                            frame.Column = sequencePoint.Start.column;

                            if (sequencePoint.Start != sequencePoint.End)
                            {
                                frame.EndLine = sequencePoint.End.line;
                                frame.EndColumn = sequencePoint.End.column;
                            }
                        }
                    }

                    yield return frame;
                }
            }
        }

        public IEnumerable<Scope> GetScopes(ScopesArguments args)
        {
            if ((engine.State & HALT_OR_FAULT) == 0)
            {
                var context = engine.InvocationStack.ElementAt(args.FrameId);
                // TODO: ExecutionContext needs a mechanism to retrieve script hash 
                //       https://github.com/neo-project/neo/issues/1696
                var scriptHash = Neo.SmartContract.Helper.ToScriptHash(context.Script);

                if (disassemblyView)
                {
                    yield return AddScope("Evaluation Stack", new EvaluationStackContainer(context.EvaluationStack));
                    yield return AddScope("Locals", new SlotContainer("local", context.LocalVariables));
                    yield return AddScope("Statics", new SlotContainer("static", context.StaticFields));
                    yield return AddScope("Arguments", new SlotContainer("arg", context.Arguments));
                }
                else
                {
                    var debugInfo = debugInfoMap.TryGetValue(scriptHash, out var di) ? di : null;
                    yield return AddScope("Variables", new ExecutionContextContainer(context, debugInfo));
                }

                yield return AddScope("Storage", new StorageContainer(scriptHash, engine.Snapshot));
            }

            Scope AddScope(string name, IVariableContainer container)
            {
                var reference = variableManager.Add(container);
                return new Scope(name, reference, false);
            }
        }

        public IEnumerable<Variable> GetVariables(VariablesArguments args)
        {
            if ((engine.State & HALT_OR_FAULT) == 0)
            {
                if (variableManager.TryGet(args.VariablesReference, out var container))
                {
                    return container.Enumerate(variableManager);
                }
            }

            return Enumerable.Empty<Variable>();
        }

        public SourceResponse GetSource(SourceArguments arguments)
        {
            if (disassemblyManager.TryGetDisassembly(arguments.SourceReference, out var disassembly))
            {
                return new SourceResponse(disassembly.Source)
                {
                    MimeType = "text/x-neovm.disassembly"
                };
            }

            throw new InvalidOperationException();
        }

        Variable? Evaluate(ExecutionContext context, string expression, string typeHint)
        {

            if (expression.StartsWith("#arg"))
            {
                return EvaluateSlot(context.Arguments, expression, 4);
            }

            if (expression.StartsWith("#local"))
            {
                return EvaluateSlot(context.LocalVariables, expression, 6);
            }

            if (expression.StartsWith("#static"))
            {
                return EvaluateSlot(context.StaticFields, expression, 7);
            }

            // TODO: ExecutionContext needs a mechanism to retrieve script hash 
            //       https://github.com/neo-project/neo/issues/1696
            var scriptHash = Neo.SmartContract.Helper.ToScriptHash(context.Script);

            if (expression.StartsWith("#storage"))
            {
                var container = new StorageContainer(scriptHash, engine.Snapshot);
                return container.Evaluate(variableManager, expression, typeHint);
            }

            if (debugInfoMap.TryGetValue(scriptHash, out var debugInfo)
                && debugInfo.TryGetMethod(context.InstructionPointer, out var method))
            {
                Variable? response;
                if (TryEvaluateSlot(context.Arguments, method.Parameters, out response))
                {
                    return response;
                }
                
                if (TryEvaluateSlot(context.LocalVariables, method.Variables, out response))
                {
                    return response;
                }
            }

            return null;

            bool TryEvaluateSlot(Slot slot, IList<(string name, string type)> variables, out Variable? response)
            {
                for (int i = 0; i < variables.Count; i++)
                {
                    if (i < slot.Count)
                    {
                        var (name, type) = variables[i];
                        if (name == expression)
                        {
                            response = slot[i].ToVariable(variableManager, name, 
                                string.IsNullOrEmpty(typeHint) ? type : typeHint);
                            return true;
                        }
                    }
                }

                response = null!;
                return false;
            }

            Variable? EvaluateSlot(Slot slot, string name, int count)
            {
                if (int.TryParse(name.AsSpan().Slice(count), out int index)
                    && index < slot.Count)
                {
                    return slot[index]
                        .ToVariable(this.variableManager, name, typeHint);
                }

                return null;
            }
        }

        public EvaluateResponse Evaluate(EvaluateArguments args)
        {
            if (!args.FrameId.HasValue)
                return DebugAdapter.FailedEvaluation;

            var context = engine.InvocationStack.ElementAt(args.FrameId.Value);
            var (typeHint, expression) = VariableManager.ParsePrefix(args.Expression);

            var variable = Evaluate(context, expression, typeHint);

            if (variable != null)
                return variable.ToEvaluateResponse();

            return DebugAdapter.FailedEvaluation;
        }

        public IEnumerable<Breakpoint> SetBreakpoints(Source source, IReadOnlyList<SourceBreakpoint> sourceBreakpoints)
        {
            return breakpointManager.SetBreakpoints(source, sourceBreakpoints);
        }

        public void SetDebugView(DebugView debugView)
        {
            var original = disassemblyView;
            disassemblyView = debugView switch
            {
                DebugView.Disassembly => true,
                DebugView.Source => false,
                DebugView.Toggle => !disassemblyView,
                _ => throw new ArgumentException(nameof(debugView))
            };

            if (original != disassemblyView)
            {
                FireStoppedEvent(StoppedEvent.ReasonValue.Step);
            }
        }

        private void FireStoppedEvent(StoppedEvent.ReasonValue reasonValue)
        {
            variableManager.Clear();

            if ((engine.State & VMState.FAULT) != 0)
            {
                sendEvent(new OutputEvent()
                {
                    Category = OutputEvent.CategoryValue.Stderr,
                    Output = "Engine State Faulted\n",
                });
                sendEvent(new TerminatedEvent());
            }
            if ((engine.State & VMState.HALT) != 0)
            {
                for (var i = 0; i < engine.ResultStack.Count; i++)
                {
                    var result = engine.ResultStack.Peek(i);
                    sendEvent(new OutputEvent()
                    {
                        Category = OutputEvent.CategoryValue.Stdout,
                        Output = $"Return: {result.ToResult()}\n",
                    });
                }
                sendEvent(new ExitedEvent());
                sendEvent(new TerminatedEvent());
            }
            else
            {
                sendEvent(new StoppedEvent(reasonValue) { ThreadId = 1 });
            }
        }

        void Step(Func<int, int, bool> compare)
        {
            var originalStackCount = engine.InvocationStack.Count;
            var stopReason = StoppedEvent.ReasonValue.Step;
            while ((engine.State & HALT_OR_FAULT) == 0)
            {
                engine.ExecuteInstruction();

                if (engine.CurrentContext != null && 
                    breakpointManager.CheckBreakpoint(engine.CurrentScriptHash, engine.CurrentContext.InstructionPointer))
                {
                    stopReason = StoppedEvent.ReasonValue.Breakpoint;
                    break;
                }

                if (compare(engine.InvocationStack.Count, originalStackCount)
                    && (disassemblyView || CheckSequencePoint()))
                {
                    break;
                }
            }

            FireStoppedEvent(stopReason);

            bool CheckSequencePoint()
            {
                if (engine.CurrentContext != null) 
                {
                    var ip = engine.CurrentContext.InstructionPointer;
                    if (debugInfoMap.TryGetValue(engine.CurrentScriptHash, out var info))
                    {
                        var methods = info.Methods;
                        for (int i = 0; i < methods.Count; i++)
                        {
                            if (methods[i].SequencePoints.Any(sp => sp.Address == ip))
                            {
                                return true;
                            }
                        }
                    }
                }
                return false;
            } 
        }

        public void Continue()
        {
            while ((engine.State & HALT_OR_FAULT) == 0)
            {
                engine.ExecuteInstruction();

                if (engine.CurrentContext != null && 
                    breakpointManager.CheckBreakpoint(engine.CurrentScriptHash, engine.CurrentContext.InstructionPointer))
                {
                    break;
                }
            }

            FireStoppedEvent(StoppedEvent.ReasonValue.Breakpoint);
        }

        public void StepIn()
        {
            Step((_, __) => true);
        }

        public void StepOut()
        {
            Step((currentStackCount, originalStackCount) => currentStackCount < originalStackCount);
        }

        public void StepOver()
        {
            Step((currentStackCount, originalStackCount) => currentStackCount <= originalStackCount);
        }
    }
}
