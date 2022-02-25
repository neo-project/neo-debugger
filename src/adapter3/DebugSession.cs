using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo;
using Neo.BlockchainToolkit.Models;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.VM;

namespace NeoDebug.Neo3
{
    using ByteString = Neo.VM.Types.ByteString;
    using NeoArray = Neo.VM.Types.Array;
    using StackItem = Neo.VM.Types.StackItem;
    using StackItemType = Neo.VM.Types.StackItemType;

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

            if (engine.SupportsStepBack)
            {
                sendEvent(new CapabilitiesEvent
                {
                    Capabilities = new Capabilities { SupportsStepBack = true }
                });
            }
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
                if (scriptHash == context.ScriptIdentifier)
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
                var scriptId = context.ScriptHash;
                DebugInfo.Method? method = null;
                if (debugInfoMap.TryGetValue(context.ScriptIdentifier, out var debugInfo))
                {
                    method = debugInfo.GetMethod(context.InstructionPointer);
                }

                ContractState? contract = engine is DebugApplicationEngine debugEngine
                    ? NativeContract.ContractManagement.GetContract(debugEngine.Snapshot, context.ScriptHash)
                    : null;

                var methodName = method?.Name ?? $"<frame {engine.InvocationStack.Count - index}>";
                var frame = new StackFrame()
                {
                    Id = index,
                    Name = contract is not null 
                        ? $"{methodName} ({contract.Manifest.Name})" 
                        : methodName,
                };

                if (disassemblyView)
                {
                    var disassembly = disassemblyManager.GetDisassembly(context, debugInfo);
                    frame.Source = new Source()
                    {
                        Name = $"{context.ScriptHash}",
                        SourceReference = disassembly.SourceReference
                    };
                    frame.Line = disassembly.AddressMap[context.InstructionPointer];
                    frame.Column = 1;
                }
                else
                {
                    var sequencePoint = method?.GetCurrentSequencePoint(context.InstructionPointer);
                    var document = sequencePoint?.GetDocumentPath(debugInfo);

                    frame.Source = new Source()
                    {
                        Name = document is not null
                            ? System.IO.Path.GetFileName(document)
                            : "<null>",
                        Origin = $"ScriptHash: {context.ScriptHash}",
                        Path = document ?? string.Empty,
                    };

                    if (sequencePoint is not null)
                    {
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

        const string EVAL_STACK_PREFIX = "#eval";
        const string RESULT_STACK_PREFIX = "#result";
        public const string STORAGE_PREFIX = "#storage";
        public const string ARG_SLOTS_PREFIX = "#arg";
        public const string LOCAL_SLOTS_PREFIX = "#local";
        public const string STATIC_SLOTS_PREFIX = "#static";

        public IEnumerable<Scope> GetScopes(ScopesArguments args)
        {
            var context = engine.InvocationStack.ElementAt(args.FrameId);

            if (disassemblyView)
            {
                yield return AddScope("Evaluation Stack", new SlotContainer(EVAL_STACK_PREFIX, context.EvaluationStack));
                yield return AddScope("Arguments", new SlotContainer(ARG_SLOTS_PREFIX, context.Arguments));
                yield return AddScope("Locals", new SlotContainer(LOCAL_SLOTS_PREFIX, context.LocalVariables));
                yield return AddScope("Statics", new SlotContainer(STATIC_SLOTS_PREFIX, context.StaticFields));
                yield return AddScope("Result Stack", new SlotContainer(RESULT_STACK_PREFIX, engine.ResultStack));
            }
            else
            {
                var debugInfo = debugInfoMap.TryGetValue(context.ScriptIdentifier, out var _debugInfo) ? _debugInfo : null;
                var container = new ExecutionContextContainer(context, debugInfo);
                yield return AddScope("Variables", container);
            }

            yield return AddScope("Storage", engine.GetStorageContainer(context.ScriptHash), true);
            yield return AddScope("Engine", new EngineContainer(engine, context));

            Scope AddScope(string name, IVariableContainer container, bool expensive = false)
            {
                var reference = variableManager.Add(container);
                return new Scope(name, reference, expensive);
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

        public EvaluateResponse Evaluate(EvaluateArguments args)
        {
            if (args.FrameId.HasValue)
            {
                var context = engine.InvocationStack.ElementAt(args.FrameId.Value);
                var debugInfo = debugInfoMap.TryGetValue(context.ScriptIdentifier, out var _debugInfo) ? _debugInfo : null;
                var evaluator = new ExpressionEvaluator(engine, context, debugInfo);

                if (evaluator.TryEvaluate(variableManager, args, out var response)) return response;
            }

            return new EvaluateResponse("Evaluation failed", 0).AsFailedEval();
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
            return item?.GetString() ?? throw new InvalidOperationException("missing exception information");
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

        // string ToResult(int index)
        // {
        //     if (index >= engine.ResultStack.Count) throw new ArgumentException("", nameof(index));

        //     var result = engine.ResultStack[index];
        //     var returnType = index < returnTypes.Count ? returnTypes[index] : CastOperation.None;
        //     var (value, variablesRef) = EvaluateVariable(result, ContractParameterType.Any, returnType);

        //     return variablesRef == 0
        //         ? value
        //         : result.ToJson().ToString(Newtonsoft.Json.Formatting.Indented);
        // }

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
                    output += $"Gas Consumed: {engine.GasConsumedAsBigDecimal}\n";

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
                        // TODO: revert this commenting out
                        // var result = ToResult(index);
                        sendEvent(new OutputEvent()
                        {
                            Category = OutputEvent.CategoryValue.Stdout,
                            // Output = $"Gas Consumed: {engine.GasConsumedAsBigDecimal}\nReturn: {result}\n",
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

                if (breakpointManager.CheckBreakpoint(engine.CurrentContext?.ScriptHash ?? UInt160.Zero, engine.CurrentContext?.InstructionPointer))
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
                    if (debugInfoMap.TryGetValue(engine.CurrentContext.ScriptIdentifier, out var info))
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
