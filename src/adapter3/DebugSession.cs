using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo;
using Neo.BlockchainToolkit.Models;
using Neo.VM;

namespace NeoDebug.Neo3
{
    using NeoArray = Neo.VM.Types.Array;
    using StackItem = Neo.VM.Types.StackItem;

    internal class DebugSession : IDebugSession, IDisposable
    {
        public const string CAUGHT_EXCEPTION_FILTER = "caught";
        public const string UNCAUGHT_EXCEPTION_FILTER = "uncaught";

        private readonly IApplicationEngine engine;
        private readonly IReadOnlyList<CastOperation> returnTypes;
        private readonly IReadOnlyDictionary<UInt160, DebugInfo> debugInfoMap;
        private readonly Action<DebugEvent> sendEvent;
        private bool disassemblyView;
        private readonly DisassemblyManager disassemblyManager;
        private readonly VariableManager variableManager = new VariableManager();
        private readonly BreakpointManager breakpointManager;
        private bool breakOnCaughtExceptions;
        private bool breakOnUncaughtExceptions = true;

        public DebugSession(IApplicationEngine engine, IReadOnlyList<DebugInfo> debugInfoList, IReadOnlyList<CastOperation> returnTypes, Action<DebugEvent> sendEvent, DebugView defaultDebugView)
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

        private void OnNotify(object? sender, (UInt160 scriptHash, string scriptName, string eventName, NeoArray state) args)
        {
            var state = args.state.ToJson().ToString(Newtonsoft.Json.Formatting.Indented);
            sendEvent(new OutputEvent()
            {
                Output = $"Runtime.Notify: {(string.IsNullOrEmpty(args.scriptName) ? args.scriptHash.ToString() : args.scriptName)} {args.eventName} {state}\n",
            });
        }

        private void OnLog(object? sender, (UInt160 scriptHash, string scriptName, string message) args)
        {
            sendEvent(new OutputEvent()
            {
                Output = $"Runtime.Log: {(string.IsNullOrEmpty(args.scriptName) ? args.scriptHash.ToString() : args.scriptName)} {args.message}\n",
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
                var scriptId = context.ScriptIdentifier;
                DebugInfo.Method? method = null;
                if (debugInfoMap.TryGetValue(context.ScriptHash, out var debugInfo))
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
                    var disassembly = disassemblyManager.GetDisassembly(context, debugInfo);
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
            var scriptId = context.ScriptIdentifier;

            if (disassemblyView)
            {
                yield return AddScope("Evaluation Stack", new SlotContainer("eval", context.EvaluationStack));
                yield return AddScope("Arguments", new SlotContainer("arg", context.Arguments));
                yield return AddScope("Locals", new SlotContainer("local", context.LocalVariables));
                yield return AddScope("Statics", new SlotContainer("static", context.StaticFields));
                yield return AddScope("Result Stack", new SlotContainer("result", engine.ResultStack));
            }
            else
            {
                var debugInfo = debugInfoMap.TryGetValue(context.ScriptHash, out var _debugInfo) ? _debugInfo : null;
                var container = new ExecutionContextContainer(context, debugInfo);
                yield return AddScope("Variables", container);
            }

            yield return AddScope("Storage", engine.GetStorageContainer(scriptId));

            Scope AddScope(string name, IVariableContainer container)
            {
                var reference = variableManager.AddContainer(container);
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
            if (name.Length > 0 && name.Span[0] == '#')
            {
                if (name.StartsWith("#arg")) return (EvaluateSlot(context.Arguments, 4), string.Empty);
                if (name.StartsWith("#eval")) return (EvaluateSlot(context.EvaluationStack, 5), string.Empty);
                if (name.StartsWith("#local")) return (EvaluateSlot(context.LocalVariables, 6), string.Empty);
                if (name.StartsWith("#result")) return (EvaluateSlot(engine.ResultStack, 7), string.Empty);
                if (name.StartsWith("#static")) return (EvaluateSlot(context.StaticFields, 7), string.Empty);
                if (name.StartsWith("#storage"))
                {
                    var container = engine.GetStorageContainer(context.ScriptIdentifier);
                    return (container.Evaluate(name), string.Empty);
                }
            }

            if (debugInfoMap.TryGetValue(context.ScriptHash, out var debugInfo)
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

        public static readonly IReadOnlyDictionary<string, CastOperation> CastOperations = new Dictionary<string, CastOperation>()
            {
                { "int", CastOperation.Integer },
                { "integer", CastOperation.Integer },
                { "bool", CastOperation.Boolean },
                { "boolean", CastOperation.Boolean },
                { "string", CastOperation.String },
                { "str", CastOperation.String },
                { "hex", CastOperation.HexString },
                { "byte[]", CastOperation.ByteArray },
                { "addr", CastOperation.Address },
            }.ToImmutableDictionary();

         static (ReadOnlyMemory<char> name, CastOperation castOperation, ReadOnlyMemory<char> remaining) ParseEvalExpression(string expression)
        {
            var (castOperation, text) = ParsePrefix(expression);
            if (text.StartsWith("#storage"))
            {
                return (text, castOperation, default);
            }
            var (name, remaining) = ParseName(text);
            return (name, castOperation, remaining);

            static (CastOperation castOperation, ReadOnlyMemory<char> text) ParsePrefix(string expression)
            {
                if (expression[0] == '(')
                {
                    foreach (var kvp in CastOperations)
                    {
                        if (expression.Length > kvp.Key.Length + 2
                            && expression.AsSpan().Slice(1, kvp.Key.Length).SequenceEqual(kvp.Key)
                            && expression[kvp.Key.Length + 1] == ')')
                        {
                            return (kvp.Value, expression.AsMemory().Slice(kvp.Key.Length + 2));
                        }
                    }

                    throw new Exception("invalid cast operation");
                }

                return (CastOperation.None, expression.AsMemory());
            }

            static (ReadOnlyMemory<char> name, ReadOnlyMemory<char> remaining) ParseName(ReadOnlyMemory<char> expression)
            {
                for (int i = 0; i < expression.Length; i++)
                {
                    char c = expression.Span[i];
                    if (c == '.' || c == '[')
                    {
                        return (expression.Slice(0, i), expression.Slice(i));
                    }
                }

                return (expression, default);
            }
        }

        public EvaluateResponse Evaluate(EvaluateArguments args)
        {
            if (!args.FrameId.HasValue)
                return DebugAdapter.FailedEvaluation;

            try
            {
                var context = engine.InvocationStack.ElementAt(args.FrameId.Value);
                var (name, castOp, remaining) = ParseEvalExpression(args.Expression);
                var (item, type) = Evaluate(context, name);
                (item, type) = EvaluateRemaining(item, type, remaining);

                if (item != null)
                {
                    var variable = item.ToVariable(variableManager, string.Empty);
                    return new EvaluateResponse(variable.Value, variable.VariablesReference);
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
            var item = engine.CurrentContext?.EvaluationStack[0] ?? throw new InvalidOperationException("missing exception information");
            return Neo.Utility.StrictUTF8.GetString(item.GetSpan());
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
                if (stepBack && engine.AtStart)
                {
                    sendEvent(new OutputEvent()
                    {
                        Category = OutputEvent.CategoryValue.Stdout,
                        Output = "Reached start of trace"
                    });
                    break;
                }

                if (!stepBack && engine.State == VMState.FAULT)
                {
                    var output = "Engine State Faulted\n";
                    if (engine.FaultException != null)
                    {
                        output = $"Engine State Fault: {engine.FaultException.Message} [{engine.FaultException.GetType().Name}]\n";
                        if (engine.FaultException.InnerException != null)
                        {
                            output += $"  Contract Exception: {engine.FaultException.InnerException.Message} [{engine.FaultException.InnerException.GetType().Name}]\n";
                        }
                    }

                    sendEvent(new OutputEvent()
                    {
                        Category = OutputEvent.CategoryValue.Stderr,
                        Output = output
                    });
                    sendEvent(new TerminatedEvent());
                    break;
                }

                if (!stepBack && engine.State == VMState.HALT)
                {
                    for (int index = 0; index < engine.ResultStack.Count; index++)
                    {
                        var result = engine.ResultStack[index];
                        // var resultString = ((index < returnTypes.Count) ? result.TryConvert(returnTypes[index]) : null)
                        //     ?? result.ToJson().ToString(Newtonsoft.Json.Formatting.Indented);
                        var resultString = result.ToJson().ToString(Newtonsoft.Json.Formatting.Indented);

                        sendEvent(new OutputEvent()
                        {
                            Category = OutputEvent.CategoryValue.Stdout,
                            Output = $"Return: {resultString}\n",
                        });
                    }
                    sendEvent(new ExitedEvent());
                    sendEvent(new TerminatedEvent());
                    break;
                }

                if (stepBack)
                {
                    lastThrowAddress = -1;
                    if (!engine.ExecutePrevInstruction())
                        break;
                }
                else
                {
                    // ExecutionEngine does not provide a mechanism to halt execution
                    // after an exception is thrown but before it has been handled.
                    // The debugger needs to check if the engine is about to THROW
                    // and handle the exception breakpoints appropriately. 
                    // lastThrowAddress is used to ensure we don't perform the THROW
                    // check multiple times for a single address in a row.

                    if (engine.CurrentContext?.CurrentInstruction.OpCode == OpCode.THROW
                        && engine.CurrentContext.InstructionPointer != lastThrowAddress)
                    {
                        lastThrowAddress = engine.CurrentContext.InstructionPointer;
                        var handled = engine.CatchBlockOnStack();
                        if ((handled && breakOnCaughtExceptions)
                            || (!handled && breakOnUncaughtExceptions))
                        {
                            FireStoppedEvent(StoppedEvent.ReasonValue.Exception);
                            break;
                        }
                    }
                    else
                    {
                        lastThrowAddress = -1;
                        if (!engine.ExecuteNextInstruction())
                            break;
                    }
                }

                if (breakpointManager.CheckBreakpoint(engine.CurrentContext?.ScriptIdentifier ?? UInt160.Zero, engine.CurrentContext?.InstructionPointer))
                {
                    FireStoppedEvent(StoppedEvent.ReasonValue.Breakpoint);
                    break;
                }

                if (compareStepDepth(engine.InvocationStack.Count)
                    && (disassemblyView || CheckSequencePoint()))
                {
                    FireStoppedEvent(StoppedEvent.ReasonValue.Step);
                    break;
                }
            }

            bool CheckSequencePoint()
            {
                if (engine.CurrentContext != null)
                {
                    var ip = engine.CurrentContext.InstructionPointer;
                    if (debugInfoMap.TryGetValue(engine.CurrentContext.ScriptHash, out var info))
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
            // if we are in source view and we've stepped back before the first sequence point
            // step forward to first sequence point
            if (!disassemblyView && engine.AtStart) StepIn();
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
