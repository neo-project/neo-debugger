using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo;
using Neo.BlockchainToolkit;
using Neo.BlockchainToolkit.Models;
using Neo.SmartContract.Native;
using Neo.VM;

namespace NeoDebug.Neo3
{
    using NeoArray = Neo.VM.Types.Array;

    internal class DebugSession : IDebugSession, IDisposable
    {
        public const string CAUGHT_EXCEPTION_FILTER = "caught";
        public const string UNCAUGHT_EXCEPTION_FILTER = "uncaught";

        private readonly IApplicationEngine engine;
        private readonly IReadOnlyList<CastOperation> returnTypes;
        
        private readonly IReadOnlyList<DebugInfo> debugInfoList;
        private readonly Dictionary<UInt160, DebugInfo> debugInfoMap = new();
        private readonly Dictionary<UInt160, string> contractNameMap = new();
        private readonly Action<DebugEvent> sendEvent;
        private bool disassemblyView;
        private readonly StorageView storageView;
        private readonly DisassemblyManager disassemblyManager;
        private readonly VariableManager variableManager = new VariableManager();
        private readonly BreakpointManager breakpointManager;
        private bool breakOnCaughtExceptions;
        private bool breakOnUncaughtExceptions = true;

        public DebugSession(IApplicationEngine engine,
                            IReadOnlyList<DebugInfo> debugInfoList,
                            IReadOnlyList<CastOperation> returnTypes,
                            Action<DebugEvent> sendEvent,
                            DebugView defaultDebugView,
                            StorageView storageView)
        {
            this.engine = engine;
            this.returnTypes = returnTypes;
            this.sendEvent = sendEvent;
            this.debugInfoList = debugInfoList;
            disassemblyView = defaultDebugView == DebugView.Disassembly;
            this.storageView = storageView;
            disassemblyManager = new DisassemblyManager();
            breakpointManager = new BreakpointManager(disassemblyManager, () => debugInfoMap);

            this.engine.DebugNotify += OnNotify;
            this.engine.DebugLog += OnLog;

            if (engine.SupportsStepBack)
            {
                sendEvent(new CapabilitiesEvent
                {
                    Capabilities = new Capabilities { SupportsStepBack = true }
                });
            }

            // TODO: implement for Trace Engine
            if (engine is DebugApplicationEngine debugEngine)
            {
                foreach (var contract in NativeContract.ContractManagement.ListContracts(debugEngine.Snapshot))
                {
                    contractNameMap.Add(contract.Hash, contract.Manifest.Name);

                    if (contract.Id < 0) continue;

                    var scriptId = Neo.SmartContract.Helper.ToScriptHash(contract.Nef.Script);

                    if (debugInfoList.TryFind(di => di.ScriptHash == scriptId, out var debugInfo))
                    {
                        debugInfoMap.Add(contract.Hash, debugInfo);
                        disassemblyManager.GetOrAdd(contract, debugInfo);
                    }
                    else
                    {
                        disassemblyManager.GetOrAdd(contract, null);
                    }
                }
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

        private bool TryGetDebugInfo(IExecutionContext context, [MaybeNullWhen(false)] out DebugInfo info)
        {
            if (debugInfoMap.TryGetValue(context.ScriptHash, out info))
            {
                return true;
            }

            info = default;
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
                var debugInfo = TryGetDebugInfo(context, out var _debugInfo) ? _debugInfo : null;

                DebugInfo.Method method = default;
                debugInfo?.TryGetMethod(context.InstructionPointer, out method);
                var methodName = string.IsNullOrEmpty(method.Name)
                     ? $"<frame {engine.InvocationStack.Count - index}>"
                     : method.Name;

                var contractName = contractNameMap.TryGetValue(context.ScriptHash, out var _name)
                    ? _name : null;
                
                var frame = new StackFrame()
                {
                    Id = index,
                    Name = methodName,
                };

                if (disassemblyView)
                {
                    var disassembly = disassemblyManager.GetOrAdd(context, debugInfo);
                    var shortHash = context.ScriptHash.ToString().Substring(0, 8) + "...";
                    frame.Source = new Source()
                    {
                        // As per DAP docs, Path *should* be optional when specifying SourceReference.
                        // However, in practice VSCode 1.65 doesn't render bound disassembly breakpoints
                        // correctly when Path is not set and Name appears to be ignored
                        Path = $"{contractName ?? shortHash} disassembly",
                        SourceReference = disassembly.SourceReference,
                        // AdapterData = disassembly.LineMap,
                        Origin = $"{disassembly.ScriptHash}",
                        PresentationHint = contractName is null
                            ? Source.PresentationHintValue.Deemphasize
                            : Source.PresentationHintValue.Normal,
                    };
                    frame.Line = disassembly.AddressMap[context.InstructionPointer];
                    frame.Column = 1;
                }
                else
                {
                    if (method.TryGetSequencePoint(context.InstructionPointer, out var point) 
                        && point.TryGetDocumentPath(debugInfo, out var docPath))
                    {
                        frame.Source = new Source()
                        {
                            Name = System.IO.Path.GetFileName(docPath),
                            Origin = string.IsNullOrEmpty(contractName)
                                ? $"{context.ScriptHash}"
                                : $"{contractName} ({context.ScriptHash})",
                            Path = docPath,
                        };

                        frame.Line = point.Start.line;
                        frame.Column = point.Start.column;

                        if (point.Start != point.End)
                        {
                            frame.EndLine = point.End.line;
                            frame.EndColumn = point.End.column;
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
            var debugInfo = TryGetDebugInfo(context, out var _debugInfo) ? _debugInfo : null;

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
                var container = new ExecutionContextContainer(context, debugInfo, engine.AddressVersion);
                yield return AddScope("Variables", container);
            }

            yield return AddScope("Storage", engine.GetStorageContainer(context.ScriptHash, debugInfo?.StorageGroups, storageView), true);
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
            if (arguments.Source.SourceReference.HasValue
                && disassemblyManager.TryGet(arguments.Source.SourceReference.Value, out var disassembly))
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
                var debugInfo = TryGetDebugInfo(context, out var _debugInfo) ? _debugInfo : null;
                var evaluator = new ExpressionEvaluator(engine, context, debugInfo, storageView);

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

        string ToResult(int index)
        {
            if (index >= engine.ResultStack.Count) throw new ArgumentException("", nameof(index));

            var result = engine.ResultStack[index];
            var returnType = index < returnTypes.Count ? returnTypes[index] : CastOperation.None;

            // TODO: use manifest/debug info to automatically determine result type
            // var (value, variablesRef) = EvaluateVariable(result, ContractParameterType.Any, returnType);

            return result.ToJson().ToString(Newtonsoft.Json.Formatting.Indented);
        }

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
                        var result = ToResult(index);
                        sendEvent(new OutputEvent()
                        {
                            Category = OutputEvent.CategoryValue.Stdout,
                            Output = $"Gas Consumed: {engine.GasConsumedAsBigDecimal}\nReturn: {result}\n",
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

                if (breakpointManager.CheckBreakpoint(engine.CurrentContext))
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
                    if (TryGetDebugInfo(engine.CurrentContext, out var info))
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
