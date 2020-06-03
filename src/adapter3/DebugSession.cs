using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo.VM;
using NeoDebug;

namespace NeoDebug.Neo3
{
    class DebugSession : IDebugSession, IDisposable
    {
        private readonly DebugExecutionEngine engine;
        private readonly Action<DebugEvent> sendEvent;

        public DebugSession(DebugExecutionEngine engine, Action<DebugEvent> sendEvent)
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

        const VMState HALT_OR_FAULT = VMState.HALT | VMState.FAULT;

        static IEnumerable<(int ip, Instruction instruction)> EnumerateInstructions(Neo.VM.Script script)
        {
            int ip = 0;
            while (ip < script.Length)
            {
                var instruction = script.GetInstruction(ip);
                yield return (ip, instruction);
                ip = ip + instruction.Size;
            }
        }

        private readonly Dictionary<int, ImmutableDictionary<int, int>> sourceMaps = new Dictionary<int, ImmutableDictionary<int, int>>();
        private readonly Dictionary<int, string> sources = new Dictionary<int, string>();

        int GetDisassemblyLine(Neo.VM.Script script, int ip)
        {
            var hash = script.GetHashCode();
            var stringBuilder = new System.Text.StringBuilder();
            if (!sourceMaps.TryGetValue(hash, out var map))
            {
                int line = 1;
                var builder = ImmutableDictionary.CreateBuilder<int, int>();
                foreach (var t in EnumerateInstructions(script))
                {
                    stringBuilder.Append($"{line} {t.ip} {t.instruction.OpCode}\n");
                    builder.Add(t.ip, line++);
                }
                map = builder.ToImmutable();
                sourceMaps[hash] = map;
                sources[hash] = stringBuilder.ToString();
            }

            return map[ip];
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

                    yield return new StackFrame
                    {
                        Source = new Source()
                        {
                            SourceReference = hashCode,
                            Name = hashCode.ToString(),
                            Path = hashCode.ToString(),
                            AdapterData = id,
                        },
                        Line = GetDisassemblyLine(context.Script, context.InstructionPointer)
                    };
                }
            }
        }

        public IEnumerable<Scope> GetScopes(ScopesArguments args)
        {
            yield break;
        }

        public IEnumerable<Variable> GetVariables(VariablesArguments args)
        {
            yield break;
        }

        public SourceResponse GetSource(SourceArguments arguments)
        {
            var source = sources[arguments.SourceReference];
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
