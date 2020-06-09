using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo.VM;
using NeoDebug;

namespace NeoDebug.Neo3
{

    class DebugSession : IDebugSession, IDisposable
    {
        const VMState HALT_OR_FAULT = VMState.HALT | VMState.FAULT;

        private readonly DebugApplicationEngine engine;
        private readonly Action<DebugEvent> sendEvent;
        private readonly DisassemblyManager disassemblyManager = new DisassemblyManager();
        private readonly VariableManager variableManager = new VariableManager();

        public DebugSession(DebugApplicationEngine engine, Action<DebugEvent> sendEvent)
        {
            this.engine = engine;
            this.sendEvent = sendEvent;
        }

        public void Dispose()
        {
            engine.Dispose();
        }

        public void Start()
        {
            sendEvent(new StoppedEvent(StoppedEvent.ReasonValue.Entry) { ThreadId = 1 });
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
                    var name = Neo.SmartContract.Helper.ToScriptHash(context.Script).ToString();
                    var line = disassemblyManager.GetLine(context.Script, context.InstructionPointer);
                    var sourceRef = disassemblyManager.GetSourceReference(context.Script);

                    yield return new StackFrame
                    {
                        Id = index,
                        Name = $"frame {engine.InvocationStack.Count - index}",
                        Line = index == 0 ? line : line - 1,
                        Source = new Source()
                        {
                            SourceReference = sourceRef,
                            Name = name,
                            Path = name,
                        },
                    };
                }
            }
        }

        public IEnumerable<Scope> GetScopes(ScopesArguments args)
        {
            variableManager.Clear();

            if ((engine.State & HALT_OR_FAULT) == 0)
            {
                var context = engine.InvocationStack.ElementAt(args.FrameId);

                yield return AddScope("Evaluation Stack", new EvaluationStackContainer(variableManager, context.EvaluationStack));

                if (context.LocalVariables != null) 
                {
                    yield return AddScope("Locals", new SlotContainer(variableManager, context.LocalVariables));
                }
                if (context.StaticFields != null) 
                {
                    yield return AddScope("Statics", new SlotContainer(variableManager, context.StaticFields));
                }
                if (context.Arguments != null) 
                {
                    yield return AddScope("Arguments", new SlotContainer(variableManager, context.Arguments));
                }
            }

            Scope AddScope(string name, IVariableContainer container)
            {
                var @ref = variableManager.Add(container);
                return new Scope(name, @ref, false);
            }
        }

        public IEnumerable<Variable> GetVariables(VariablesArguments args)
        {
            if ((engine.State & HALT_OR_FAULT) == 0)
            {
                if (variableManager.TryGet(args.VariablesReference, out var container))
                {
                    return container.Enumerate();
                }
            }

            return Enumerable.Empty<Variable>();
        }

        public SourceResponse GetSource(SourceArguments arguments)
        {
            var source = disassemblyManager.GetSource(arguments.SourceReference);
            return new SourceResponse(source)
            {
                MimeType = "text/x-neovm.disassembly"
            };
        }

        public EvaluateResponse Evaluate(EvaluateArguments args)
        {
            return DebugAdapter.FailedEvaluation;
        }

        public IEnumerable<Breakpoint> SetBreakpoints(Source source, IReadOnlyList<SourceBreakpoint> sourceBreakpoints)
        {
            return Enumerable.Empty<Breakpoint>();
        }

        public void SetDebugView(DebugView debugView)
        {
        }


        private void FireStoppedEvent(StoppedEvent.ReasonValue reasonValue)
        {
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

                if ((engine.State & HALT_OR_FAULT) != 0)
                {
                    break;
                }

                if (compare(engine.InvocationStack.Count, originalStackCount))
                {
                    break;
                }
            }

            FireStoppedEvent(stopReason);
        }

        public void Continue()
        {
            while ((engine.State & HALT_OR_FAULT) == 0)
            {
                engine.ExecuteInstruction();
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
