using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo;
using Neo.Persistence;
using Neo.SmartContract;
using Neo.VM;
using NeoDebug;

namespace NeoDebug.Neo3
{
    using ByteString = Neo.VM.Types.ByteString;
    using StackItem = Neo.VM.Types.StackItem;
    using StackItemType = Neo.VM.Types.StackItemType;

    class DebugSession : IDebugSession, IDisposable
    {
        private readonly DebugApplicationEngine engine;
        private readonly IStore store;
        private readonly IReadOnlyList<string> returnTypes;
        private readonly Action<DebugEvent> sendEvent;
        private bool disassemblyView;
        private readonly DisassemblyManager disassemblyManager;
        private readonly VariableManager variableManager = new VariableManager();
        private readonly BreakpointManager breakpointManager;

        public DebugSession(DebugApplicationEngine engine, IStore store, IReadOnlyList<string> returnTypes, Action<DebugEvent> sendEvent, DebugView defaultDebugView)
        {
            this.engine = engine;
            this.store = store;
            this.returnTypes = returnTypes;
            this.sendEvent = sendEvent;
            this.disassemblyView = defaultDebugView == DebugView.Disassembly;
            this.disassemblyManager = new DisassemblyManager(TryGetScript, TryGetDebugInfo);
            this.breakpointManager = new BreakpointManager(this.disassemblyManager, () => DebugInfo.Find(store));

            this.engine.DebugNotify += OnNotify;
            this.engine.DebugLog += OnLog;
        }

        void OnNotify(object? sender, Neo.SmartContract.NotifyEventArgs args)
        {
            sendEvent(new OutputEvent()
            {
                Output = $"Runtime.Notify: {args.ScriptHash} {args.State.ToResult()}\n",
            });
        }

        void OnLog(object? sender, Neo.SmartContract.LogEventArgs args)
        {
            sendEvent(new OutputEvent()
            {
                Output = $"Runtime.Log: {args.ScriptHash} {args.Message}\n",
            });
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
                if (scriptHash == context.GetScriptHash())
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
            debugInfo = DebugInfo.TryGet(store, scriptHash)!;
            return debugInfo != null;
        }

        public void Start()
        {
            variableManager.Clear();

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

            foreach (var (context, index) in engine.InvocationStack.Select((c, i) => (c, i)))
            {
                var scriptHash = context.GetScriptHash();
                DebugInfo.Method? method = null;
                if (TryGetDebugInfo(scriptHash, out var debugInfo))
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
                    frame.Line = line; //index == 0 ? line : line - 1;   
                }
                else
                {
                    var sequencePoint = method?.GetCurrentSequencePoint(context.InstructionPointer);

                    if (sequencePoint != null)
                    {
                        var document = sequencePoint.GetDocumentPath(debugInfo);
                        if (document != null)
                        {
                            frame.Source = new Source()
                            {
                                Name = System.IO.Path.GetFileName(document),
                                Path = document
                            };
                        }
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

        public IEnumerable<Scope> GetScopes(ScopesArguments args)
        {
            var context = engine.InvocationStack.ElementAt(args.FrameId);
            var scriptHash = context.GetScriptHash();

            if (disassemblyView)
            {
                yield return AddScope("Evaluation Stack", new EvaluationStackContainer(context.EvaluationStack));
                yield return AddScope("Locals", new SlotContainer("local", context.LocalVariables));
                yield return AddScope("Statics", new SlotContainer("static", context.StaticFields));
                yield return AddScope("Arguments", new SlotContainer("arg", context.Arguments));
            }
            else
            {
                var debugInfo = TryGetDebugInfo(scriptHash, out var di) ? di : null;
                yield return AddScope("Variables", new ExecutionContextContainer(context, debugInfo));
            }

            yield return AddScope("Storage", new StorageContainer(scriptHash, engine.Snapshot));

            Scope AddScope(string name, IVariableContainer container)
            {
                var reference = variableManager.Add(container);
                return new Scope(name, reference, false);
            }
        }

        public IEnumerable<Variable> GetVariables(VariablesArguments args)
        {
            if (variableManager.TryGet(args.VariablesReference, out var container))
            {
                return container.Enumerate(variableManager);
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

        (StackItem? item, string typeHint) Evaluate(ExecutionContext context, ReadOnlyMemory<char> name)
        {
            if (name.StartsWith("#eval"))
            {
                if (int.TryParse(name.Span.Slice(5), out var value))
                {
                    var index = context.EvaluationStack.Count - 1 - value;
                    if (index >= 0 && index < context.EvaluationStack.Count)
                    {
                        return (context.EvaluationStack.Peek(index), string.Empty);
                    }
                }

                return (null, string.Empty);
            }

            if (name.StartsWith("#arg"))
            {
                return (EvaluateSlot(context.Arguments, 4), string.Empty);
            }

            if (name.StartsWith("#local"))
            {
                return (EvaluateSlot(context.LocalVariables, 6), string.Empty);
            }

            if (name.StartsWith("#static"))
            {
                return (EvaluateSlot(context.StaticFields, 7), string.Empty);
            }

            var scriptHash = context.GetScriptHash();

            if (name.StartsWith("#storage"))
            {
                var container = new StorageContainer(scriptHash, engine.Snapshot);
                return (container.Evaluate(name), string.Empty);
            }

            if (TryGetDebugInfo(scriptHash, out var debugInfo)
                && debugInfo.TryGetMethod(context.InstructionPointer, out var method))
            {
                (StackItem?, string) result;
                if (TryEvaluateSlot(context.Arguments, method.Parameters, out result))
                {
                    return result;
                }

                if (TryEvaluateSlot(context.LocalVariables, method.Variables, out result))
                {
                    return result;
                }
            }

            return default;

            bool TryEvaluateSlot(Slot slot, IReadOnlyList<(string name, string type)> variables, out (StackItem?, string) result)
            {
                for (int i = 0; i < variables.Count; i++)
                {
                    if (i < slot.Count)
                    {
                        if (name.Span.SequenceEqual(variables[i].name))
                        {
                            result = (slot[i], variables[i].type);
                            return true;
                        }
                    }
                }

                result = default;
                return false;
            }

            StackItem? EvaluateSlot(Slot slot, int count)
            {
                if (int.TryParse(name.Span.Slice(count), out int index)
                    && index < slot.Count)
                {
                    return slot[index];
                }

                return null;
            }
        }

        private static (StackItem? item, string type) EvaluateRemaining(StackItem? item, string type, ReadOnlyMemory<char> remaining)
        {
            if (remaining.IsEmpty)
            {
                return (item, type);
            }

            if (remaining.Span[0] == '[')
            {
                var bracketIndex = remaining.Span.IndexOf(']');
                if (bracketIndex >= 0
                    && int.TryParse(remaining[1..bracketIndex].Span, out var index))
                {
                    var newItem = item switch
                    {
                        Neo.VM.Types.Buffer buffer => (int)buffer.InnerBuffer[index],
                        Neo.VM.Types.ByteString byteString => (int)byteString.GetSpan()[index],
                        Neo.VM.Types.Array array => array[index],
                        _ => throw new InvalidOperationException(),
                    };

                    return EvaluateRemaining(newItem, string.Empty, remaining.Slice(bracketIndex + 1));
                }
            }

            throw new InvalidOperationException();
        }

        public EvaluateResponse Evaluate(EvaluateArguments args)
        {
            if (!args.FrameId.HasValue)
                return DebugAdapter.FailedEvaluation;

            try
            {
                var context = engine.InvocationStack.ElementAt(args.FrameId.Value);
                var (name, typeHint, remaining) = VariableManager.ParseEvalExpression(args.Expression);
                var (item, type) = Evaluate(context, name);
                (item, type) = EvaluateRemaining(item, type, remaining);

                if (item != null)
                {
                    return item
                        .ToVariable(variableManager, string.Empty, string.IsNullOrEmpty(typeHint) ? type : typeHint)
                        .ToEvaluateResponse();
                }

                return DebugAdapter.FailedEvaluation;
            }
            catch (Exception)
            {
                return DebugAdapter.FailedEvaluation;
            }
        }

        public IEnumerable<Breakpoint> SetBreakpoints(Source source, IReadOnlyList<SourceBreakpoint> sourceBreakpoints)
        {
            return breakpointManager.SetBreakpoints(source, sourceBreakpoints);
        }

        public void SetExceptionBreakpoints(IReadOnlyList<string> filters)
        {
        }

        public string GetExceptionInfo()
        {
            var item = engine.CurrentContext?.EvaluationStack.Peek();
            return item == null || item.IsNull 
                ? "<null>"
                : Encoding.UTF8.GetString(((ByteString)item.ConvertTo(StackItemType.ByteString)).GetSpan());
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
            sendEvent(new StoppedEvent(reasonValue) { ThreadId = 1 });
        }

        private void Step(Func<int, bool> compareStepDepth)
        {
            while (true)
            {
                if (engine.State == VMState.FAULT)
                {
                    sendEvent(new OutputEvent()
                    {
                        Category = OutputEvent.CategoryValue.Stderr,
                        Output = "Engine State Faulted\n",
                    });
                    sendEvent(new TerminatedEvent());
                    return;
                }

                if (engine.State == VMState.HALT)
                {
                    for (var i = 0; i < engine.ResultStack.Count; i++)
                    {
                        var typeHint = i < returnTypes.Count
                            ? returnTypes[i] : string.Empty;
                        var result = engine.ResultStack.Peek(i);
                        sendEvent(new OutputEvent()
                        {
                            Category = OutputEvent.CategoryValue.Stdout,
                            Output = $"Return: {result.ToResult(typeHint)}\n",
                        });
                    }
                    sendEvent(new ExitedEvent());
                    sendEvent(new TerminatedEvent());
                    return;
                }

                if (engine.CurrentContext?.CurrentInstruction.OpCode == OpCode.THROW)
                {
                    FireStoppedEvent(StoppedEvent.ReasonValue.Exception);
                    return;
                }

                engine.ExecuteInstruction();

                if (breakpointManager.CheckBreakpoint(engine.CurrentScriptHash, engine.CurrentContext?.InstructionPointer))
                {
                    FireStoppedEvent(StoppedEvent.ReasonValue.Breakpoint);
                    return;
                }

                if (compareStepDepth(engine.InvocationStack.Count)
                    && (disassemblyView || CheckSequencePoint()))
                {
                    FireStoppedEvent(StoppedEvent.ReasonValue.Step);
                    return;
                }
            }

            bool CheckSequencePoint()
            {
                if (engine.CurrentContext != null)
                {
                    var ip = engine.CurrentContext.InstructionPointer;
                    if (TryGetDebugInfo(engine.CurrentScriptHash, out var info))
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
            Step((_) => false);
        }

        public void StepIn()
        {
            Step((_) => true);
        }

        public void StepOut()
        {
            var originalStackCount = engine.InvocationStack.Count;
            Step((currentStackCount) => currentStackCount < originalStackCount);
        }

        public void StepOver()
        {
            var originalStackCount = engine.InvocationStack.Count;
            Step((currentStackCount) => currentStackCount <= originalStackCount);
        }
    }
}
