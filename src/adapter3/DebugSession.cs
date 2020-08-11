using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo;
using Neo.Persistence;
using Neo.VM;
using NeoArray = Neo.VM.Types.Array;

namespace NeoDebug.Neo3
{
    using StackItem = Neo.VM.Types.StackItem;

    internal class DebugSession : IDebugSession, IDisposable
    {
        public const string CAUGHT_EXCEPTION_FILTER = "caught";
        public const string UNCAUGHT_EXCEPTION_FILTER = "uncaught";

        private readonly IApplicationEngine engine;
        private readonly IReadOnlyList<string> returnTypes;
        private readonly IReadOnlyDictionary<UInt160, DebugInfo> debugInfoMap;
        private readonly Action<DebugEvent> sendEvent;
        private bool disassemblyView;
        private readonly DisassemblyManager disassemblyManager;
        private readonly VariableManager variableManager = new VariableManager();
        private readonly BreakpointManager breakpointManager;
        private bool breakOnCaughtExceptions;
        private bool breakOnUncaughtExceptions = true;

        public DebugSession(IApplicationEngine engine, IReadOnlyList<DebugInfo> debugInfoList, IReadOnlyList<string> returnTypes, Action<DebugEvent> sendEvent, DebugView defaultDebugView)
        {
            this.engine = engine;
            this.returnTypes = returnTypes;
            this.sendEvent = sendEvent;
            debugInfoMap = debugInfoList.ToDictionary(di => di.ScriptHash);
            disassemblyView = defaultDebugView == DebugView.Disassembly;
            disassemblyManager = new DisassemblyManager(TryGetScript, debugInfoMap.TryGetValue);
            breakpointManager = new BreakpointManager(disassemblyManager, debugInfoList);

            this.engine.DebugNotify += OnNotify;
            this.engine.DebugLog += OnLog;
        }

        private void OnNotify(object? sender, (UInt160 scriptHash, string eventName, NeoArray state) args)
        {
            sendEvent(new OutputEvent()
            {
                Output = $"Runtime.Notify: {args.scriptHash} {args.state.ToResult()}\n",
            });
        }

        private void OnLog(object? sender, (UInt160 scriptHash, string message) args)
        {
            sendEvent(new OutputEvent()
            {
                Output = $"Runtime.Log: {args.scriptHash} {args.message}\n",
            });
        }

        public void Dispose()
        {
            engine.Dispose();
        }

        private bool TryGetScript(UInt160 scriptHash, [MaybeNullWhen(false)] out Neo.VM.Script script)
        {
            if (engine.TryGetContract(scriptHash, out var contractScript))
            {
                script = contractScript;
                return true;
            }

            foreach (var context in engine.InvocationStack)
            {
                if (scriptHash == context.ScriptHash)
                {
                    script = context.Script;
                    return true;
                }
            }

            script = null;
            return false;
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
                var scriptHash = context.ScriptHash;
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
                    frame.Source = new Source()
                    {
                        SourceReference = disassembly.SourceReference,
                        Name = disassembly.Name,
                        Path = disassembly.Name,
                    };
                    frame.Line = disassembly.AddressMap[context.InstructionPointer];
                    frame.Column = 1;
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
            var scriptHash = context.ScriptHash;

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

            yield return AddScope("Storage", engine.GetStorageContainer(scriptHash));

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

        private (StackItem? item, string typeHint) Evaluate(IExecutionContext context, ReadOnlyMemory<char> name)
        {
            if (name.StartsWith("#eval"))
            {
                if (int.TryParse(name.Span.Slice(5), out var value))
                {
                    var index = context.EvaluationStack.Count - 1 - value;
                    if (index >= 0 && index < context.EvaluationStack.Count)
                    {
                        return (context.EvaluationStack[index], string.Empty);
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

            var scriptHash = context.ScriptHash;

            if (name.StartsWith("#storage"))
            {
                var container = engine.GetStorageContainer(scriptHash);
                return (container.Evaluate(name), string.Empty);
            }

            if (debugInfoMap.TryGetValue(scriptHash, out var debugInfo)
                && debugInfo.TryGetMethod(context.InstructionPointer, out var method))
            {
                if (TryEvaluateSlot(context.Arguments, method.Parameters, out (StackItem?, string) result))
                {
                    return result;
                }

                if (TryEvaluateSlot(context.LocalVariables, method.Variables, out result))
                {
                    return result;
                }
            }

            return default;

            bool TryEvaluateSlot(IReadOnlyList<StackItem> slot, IReadOnlyList<(string name, string type)> variables, out (StackItem?, string) result)
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

            StackItem? EvaluateSlot(IReadOnlyList<StackItem> slot, int count)
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
            breakOnCaughtExceptions = filters.Any(f => f == CAUGHT_EXCEPTION_FILTER);
            breakOnUncaughtExceptions = filters.Any(f => f == UNCAUGHT_EXCEPTION_FILTER);
        }

        public string GetExceptionInfo()
        {
            var item = engine.CurrentContext?.EvaluationStack[0];
            return item?.ToStrictUTF8String()
                ?? throw new InvalidOperationException("missing exception information");
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

        private int lastThrowAddress = -1;

        private void Step(Func<int, bool> compareStepDepth, bool stepBack = false)
        {
            while (true)
            {
                if (engine.State == VMState.FAULT)
                {
                    var output = engine.UncaughtException == null
                        ? "Engine State Faulted\n"
                        : $"Uncaught Exception: {engine.UncaughtException.ToStrictUTF8String()}\n";

                    sendEvent(new OutputEvent()
                    {
                        Category = OutputEvent.CategoryValue.Stderr,
                        Output = output
                    });
                    sendEvent(new TerminatedEvent());
                    return;
                }

                if (engine.State == VMState.HALT)
                {
                    for (int index = 0; index < engine.ResultStack.Count; index++)
                    {
                        var result = engine.ResultStack[index];
                        var typeHint = index < returnTypes.Count
                            ? returnTypes[index] : string.Empty;
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

                // ExecutionEngine does not provide a mechanism to halt execution
                // after an exception is thrown but before it has been handled.
                // The debugger needs to be able to treat the exception throw and
                // handling as separate operations. So DebugApplicationEngine inserts
                // a dummy instruction execution when it detects a THROW opcode.
                // The THROW operation address is used to ensure only a single dummy
                // instruction is executed. The ExecuteInstruction return value
                // indicates that a dummy THROW instruction has been inserted.

                bool exceptionThrown = false;
                if (stepBack)
                {
                    if (!engine.ExecutePrevInstruction())
                        break;
                }
                else
                {
                    if (engine.CurrentContext?.CurrentInstruction.OpCode == OpCode.THROW
                        && engine.CurrentContext.InstructionPointer != lastThrowAddress)
                    {
                        exceptionThrown = true;
                        lastThrowAddress = engine.CurrentContext.InstructionPointer;
                    }
                    else
                    {
                        if (!engine.ExecuteNextInstruction())
                            break;
                    }
                }

                if (exceptionThrown)
                {
                    var handled = engine.CatchBlockOnStack();
                    if ((handled && breakOnCaughtExceptions)
                        || (!handled && breakOnUncaughtExceptions))
                    {
                        FireStoppedEvent(StoppedEvent.ReasonValue.Exception);
                        return;
                    }
                }

                if (breakpointManager.CheckBreakpoint(engine.CurrentContext?.ScriptHash ?? UInt160.Zero, engine.CurrentContext?.InstructionPointer))
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
                    if (debugInfoMap.TryGetValue(engine.CurrentContext?.ScriptHash ?? UInt160.Zero, out var info))
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

        public void ReverseContinue()
        {
            Step((_) => false, true);
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

        public void StepBack()
        {
            Step((_) => true, true);
        }
    }
}
