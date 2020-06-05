using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo.VM;
using NeoDebug;

namespace NeoDebug.Neo3
{
    partial class DebugSession : IDebugSession, IDisposable
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
                int i = 0;
                foreach (var context in engine.InvocationStack)
                {
                    var id = i++;
                    var frameName = $"frame {engine.InvocationStack.Count - id}";
                    var hashCode = context.Script.GetHashCode();
                    var scriptHash = Neo.SmartContract.Helper.ToScriptHash(context.Script).ToString();

                    yield return new StackFrame
                    {
                        Source = new Source()
                        {
                            SourceReference = hashCode,
                            Name = scriptHash,
                            Path = scriptHash,
                            AdapterData = id,
                        },
                        Line = disassemblyManager.GetLine(context.Script, context.InstructionPointer)
                    };
                }
            }
        }

        public IEnumerable<Scope> GetScopes(ScopesArguments args)
        {
            variableManager.Clear();

            if ((engine.State & HALT_OR_FAULT) == 0)
            {
                var context = engine.CurrentContext;

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
            yield break;
        }

        public void SetDebugView(DebugView debugView)
        {
        }


        public void Continue()
        {
            throw new NotImplementedException();
        }

        public void StepIn()
        {
            engine.ExecuteInstruction();
            sendEvent(new StoppedEvent(StoppedEvent.ReasonValue.Step) { ThreadId = 1 });
        }

        public void StepOut()
        {
            engine.ExecuteInstruction();
            sendEvent(new StoppedEvent(StoppedEvent.ReasonValue.Step) { ThreadId = 1 });
        }

        public void StepOver()
        {
            engine.ExecuteInstruction();
            sendEvent(new StoppedEvent(StoppedEvent.ReasonValue.Step) { ThreadId = 1 });
        }
    }
}
