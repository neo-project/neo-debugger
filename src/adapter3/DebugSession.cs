using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo;
using Neo.BlockchainToolkit;
using Neo.BlockchainToolkit.Models;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.VM;

namespace NeoDebug.Neo3
{
    using NeoArray = Neo.VM.Types.Array;

    class DebugSession : IDebugSession, IDisposable
    {
        public const string CAUGHT_EXCEPTION_FILTER = "caught";
        public const string UNCAUGHT_EXCEPTION_FILTER = "uncaught";

        static readonly IReadOnlyDictionary<uint, string> sysCallNames;

        static DebugSession()
        {
            sysCallNames = Neo.SmartContract.ApplicationEngine.Services
                .ToDictionary(kvp => kvp.Value.Hash, kvp => kvp.Value.Name);
        }

        readonly record struct Disassembly
        {
            public readonly UInt160 ScriptHash { get; init; }
            public readonly string Source { get; init; }
            public readonly int SourceReference { get; init; }
            public readonly IReadOnlyDictionary<int, int> AddressMap { get; init; }
            public readonly IReadOnlyDictionary<int, int> LineMap { get; init; }
        }

        readonly IApplicationEngine engine;
        readonly Action<DebugEvent> sendEvent;
        readonly StorageView storageView;
        readonly VariableManager variableManager = new VariableManager();
        readonly Dictionary<UInt160, DebugInfo> debugInfoMap = new();
        readonly Dictionary<UInt160, string> contractNameMap = new();
        readonly Dictionary<int, Disassembly> disassemblyMap = new();
        readonly Dictionary<string, IReadOnlySet<(UInt160 hash, int position)>> sourceBreakpoints = new();
        readonly Dictionary<UInt160, IReadOnlySet<int>> disassemblyBreakpoints = new();
        readonly Dictionary<UInt160, IReadOnlySet<int>> breakpointCache = new();
        readonly IReadOnlyList<CastOperation> returnTypes;

        bool disassemblyView;
        bool breakOnCaughtExceptions;
        bool breakOnUncaughtExceptions = true;

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
            disassemblyView = defaultDebugView == DebugView.Disassembly;
            this.storageView = storageView;

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

                    var scriptId = Neo.SmartContract.Helper.ToScriptHash(contract.Nef.Script.Span);

                    if (debugInfoList.TryFind(di => di.ScriptHash == scriptId, out var debugInfo))
                    {
                        debugInfoMap.Add(contract.Hash, debugInfo);
                        disassemblyMap.GetOrAdd(contract.Hash.GetHashCode(), 
                            sourceRef => ToDisassembly(sourceRef, contract.Hash, contract.Nef.Script, contract.Nef.Tokens, debugInfo));
                    }
                    else
                    {
                        debugInfo = null;
                    }
                }
            }
        }

        // TODO: Debug info decoding of state values
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
                    var disassembly = disassemblyMap.GetOrAdd(context.ScriptHash.GetHashCode(), 
                        sourceRef => ToDisassembly(sourceRef, context.ScriptHash, context.Script, context.Tokens, debugInfo));
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

        // TODO: use consts from expr eval
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
                && disassemblyMap.TryGetValue(arguments.Source.SourceReference.Value, out var disassembly))
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
            // If SourceReference has a value, source represents a disassembly file
            if (source.SourceReference.HasValue)
            {
                if (disassemblyMap.TryGetValue(source.SourceReference.Value, out var disassembly))
                {
                    HashSet<int> breakpoints = new();
                    foreach (var sbp in sourceBreakpoints)
                    {
                        var validated = disassembly.LineMap.TryGetValue(sbp.Line, out var address);

                        breakpoints.Add(address);

                        yield return new Breakpoint(validated)
                        {
                            Column = sbp.Column,
                            Line = sbp.Line,
                            Source = source
                        };
                    }
                    disassemblyBreakpoints[disassembly.ScriptHash] = breakpoints;
                }
                else
                {
                    foreach (var sbp in sourceBreakpoints)
                    {
                        yield return new Breakpoint(false)
                        {
                            Column = sbp.Column,
                            Line = sbp.Line,
                            Source = source
                        };
                    }
                }
            }
            else
            {
                var sbpValidated = new bool[sourceBreakpoints.Count];
                HashSet<(UInt160, int)> breakpoints = new();

                foreach (var (scriptHash, debugInfo) in debugInfoMap)
                {
                    if (!TryFindDocumentIndex(debugInfo.Documents, source.Path, out var index)) continue;

                    // TODO: Cache this?
                    var pointLookup = debugInfo.Methods
                        .SelectMany(m => m.SequencePoints)
                        .Where(sp => sp.Document == index)
                        .ToLookup(sp => sp.Start.line);

                    for (int j = 0; j < sourceBreakpoints.Count; j++)
                    {
                        SourceBreakpoint? sbp = sourceBreakpoints[j];
                        var validated = pointLookup.TryLookup(sbp.Line, out var points);

                        if (validated)
                        {
                            sbpValidated[j] = true;
                            breakpoints.Add((scriptHash, points.First().Address));
                        }
                    }
                }

                this.sourceBreakpoints[source.Path] = breakpoints;

                for (int i = 0; i < sourceBreakpoints.Count; i++)
                {
                    var sbp = sourceBreakpoints[i];
                    yield return new Breakpoint(sbpValidated[i])
                    {
                        Column = sbp.Column,
                        Line = sbp.Line,
                        Source = source
                    };
                }
            }

            this.breakpointCache.Clear();

            {
                // combine source and disassembly break points into a common collection of scriptHash and position
                var srcBreakpoints = this.sourceBreakpoints.SelectMany(kvp => kvp.Value);
                var dsmBreakpoints = this.disassemblyBreakpoints.SelectMany(bp => bp.Value.Select(p => (hash: bp.Key, position: p)));
                var breakpoints = srcBreakpoints.Concat(dsmBreakpoints).GroupBy(bp => bp.hash, bp => bp.position);
                foreach (var contractGroup in breakpoints)
                {
                    this.breakpointCache[contractGroup.Key] = contractGroup
                        .Distinct()
                        .ToHashSet();
                }
            }

            static bool TryFindDocumentIndex(IReadOnlyList<string> documents, string path, out int index)
            {
                for (int i = 0; i < documents.Count; i++)
                {
                    if (documents[i].Equals(path, StringComparison.OrdinalIgnoreCase))
                    {
                        index = i;
                        return true;
                    }
                }

                index = 0;
                return false;
            }
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

                if (CheckBreakpoint(engine.CurrentContext))
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

            bool CheckBreakpoint(IExecutionContext? context)
            {
                if (context is null) return false;

                if (breakpointCache.TryGetValue(context.ScriptHash, out var set)
                    && set.Contains(context.InstructionPointer))
                {
                    return true;
                }

                return false;
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

        static Disassembly ToDisassembly(int sourceRef, UInt160 scriptHash, Neo.VM.Script script, IReadOnlyList<MethodToken> tokens, DebugInfo? debugInfo)
        {
            var padString = script.GetInstructionAddressPadding();
            var sourceBuilder = new StringBuilder();
            Dictionary<int, int> addressMap = new();
            Dictionary<int, int> lineMap = new();

            var documents = (debugInfo?.Documents ?? Array.Empty<string>())
                .Select(path => 
                {
                    var fileName = Path.GetFileName(path);
                    var lines = File.Exists(path) 
                        ? File.ReadAllLines(path) 
                        : Array.Empty<string>();
                    return (fileName, lines);
                })
                .ToArray().AsReadOnly();
            var methods = debugInfo?.Methods ?? Enumerable.Empty<DebugInfo.Method>();
            var methodStarts = methods.ToDictionary(m => m.Range.Start).AsReadOnly();
            var methodEnds = methods.ToDictionary(m => m.Range.End).AsReadOnly();
            var sequencePoints = methods.SelectMany(m => m.SequencePoints)
                .ToDictionary(s => s.Address).AsReadOnly();

            var instructions = script.EnumerateInstructions()
                .ToArray().AsReadOnly();

            var line = 1;
            for (int i = 0; i < instructions.Count; i++)
            {
                if (sourceBuilder.Length > 0) sourceBuilder.Append("\n");

                if (methodStarts.TryGetValue(instructions[i].address, out var methodStart))
                {
                    sourceBuilder.AppendLine($"# Start Method {methodStart.Namespace}.{methodStart.Name}");
                    line++;
                }

                if (sequencePoints.TryGetValue(instructions[i].address, out var sp)
                    && sp.Document < documents.Count)
                {
                    var doc = documents[sp.Document];
                    if (doc.lines.Length > sp.Start.line - 1)
                    {
                        var srcLine = doc.lines[sp.Start.line - 1];

                        if (sp.Start.column > 1) srcLine = srcLine.Substring(sp.Start.column - 1);
                        if (sp.Start.line == sp.End.line && sp.End.column > sp.Start.column)
                        {
                            srcLine = srcLine.Substring(0, sp.End.column - sp.Start.column);
                        }

                        sourceBuilder.AppendLine($"# Code {doc.fileName} line {sp.Start.line}: \"{srcLine.Trim()}\"");
                        line++;
                    }
                }

                AddSource(sourceBuilder, instructions[i].address, instructions[i].instruction, padString, tokens);
                addressMap.Add(instructions[i].address, line);
                lineMap.Add(line, instructions[i].address);
                line++;

                if (methodEnds.TryGetValue(instructions[i].address, out var methodEnd))
                {
                    sourceBuilder.Append($"\n# End Method {methodEnd.Namespace}.{methodEnd.Name}");
                    line++;
                }
            }

            return new Disassembly
            {
                ScriptHash = scriptHash,
                Source = sourceBuilder.ToString(),
                SourceReference = sourceRef,
                AddressMap = addressMap,
                LineMap = lineMap
            };

            static void AddSource(StringBuilder sourceBuilder, int address, Instruction instruction, string padString, IReadOnlyList<MethodToken> tokens)
            {
                sourceBuilder.Append($"{address.ToString(padString)} {instruction.OpCode}");
                if (!instruction.Operand.IsEmpty)
                {
                    sourceBuilder.Append($" {instruction.GetOperandString()}");
                }
                var comment = instruction.GetComment(address, tokens);
                if (comment.Length > 0)
                {
                    sourceBuilder.Append($" # {comment}");
                }
            }
        }
    }
}
