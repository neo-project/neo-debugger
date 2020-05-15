using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo.VM;
using NeoDebug.Models;
using NeoDebug.VariableContainers;
using NeoFx;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace NeoDebug
{
    class DebugSession : IVariableContainerSession
    {
        private readonly DebugExecutionEngine engine;
        private readonly IReadOnlyDictionary<UInt160, Contract> contracts;
        private readonly Action<DebugEvent> sendEvent;
        private readonly IReadOnlyList<string> returnTypes;
        private readonly Dictionary<int, IVariableContainer> variableContainers = new Dictionary<int, IVariableContainer>();
        private readonly Dictionary<(string path, UInt160 scriptHash), ImmutableHashSet<int>> breakpoints = new Dictionary<(string, UInt160), ImmutableHashSet<int>>();
        private readonly DisassemblyManager disassemblyManager;
        private bool disassemblyView = false;

        public enum DebugView
        {
            Source,
            Disassembly,
            Toggle
        }

        public DebugSession(DebugExecutionEngine engine, IEnumerable<Contract> contracts, Action<DebugEvent> sendEvent, IReadOnlyList<string> returnTypes, DebugView defaultDebugView)
        {
            this.engine = engine;
            this.sendEvent = sendEvent;
            this.contracts = contracts.ToDictionary(c => c.ScriptHash);
            this.returnTypes = returnTypes;
            this.disassemblyView = defaultDebugView == DebugView.Disassembly;
            this.disassemblyManager = new DisassemblyManager(engine.GetMethodName);

            disassemblyManager.Add((byte[])engine.EntryContext.Script);
            foreach (var c in contracts)
            {
                disassemblyManager.Add(c.Script, c.DebugInfo);
            }
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

        Contract? GetContract(ExecutionContext context)
        {
            var scriptHash = new UInt160(context.ScriptHash);
            if (contracts.TryGetValue(scriptHash, out var contract))
            {
                return contract;
            }

            return null;
        }

        public IEnumerable<Breakpoint> SetBreakpoints(Source source, IReadOnlyList<SourceBreakpoint> sourceBreakpoints)
        {
            if (UInt160.TryParse(source.Name, out var scriptHash))
            {
                return SetDisassemblyBreakpoints(scriptHash);
            }

            return SetSourceBreakpoints();

            IEnumerable<Breakpoint> SetDisassemblyBreakpoints(UInt160 scriptHash)
            {
                var breakpoints = new HashSet<int>();
                for (int i = 0; i < sourceBreakpoints.Count; i++)
                {
                    var sourceBreakPoint = sourceBreakpoints[i];
                    var ip = disassemblyManager.GetInstructionPointer(scriptHash, sourceBreakPoint.Line);

                    if (ip >= 0)
                    {
                        breakpoints.Add(ip);
                    }

                    yield return new Breakpoint()
                    {
                        Verified = ip >= 0,
                        Column = sourceBreakPoint.Column,
                        Line = sourceBreakPoint.Line,
                        Source = source
                    };
                }

                this.breakpoints[(string.Empty, scriptHash)] = breakpoints.ToImmutableHashSet();
            }

            IEnumerable<Breakpoint> SetSourceBreakpoints()
            {
                foreach (var contract in contracts.Values)
                {
                    var breakpoints = new HashSet<int>();
                    var sequencePoints = contract.DebugInfo.Methods.SelectMany(m => m.SequencePoints)
                        .Where(sp => sp.Document.Equals(source.Path, StringComparison.InvariantCultureIgnoreCase))
                        .ToArray();

                    for (int j = 0; j < sourceBreakpoints.Count; j++)
                    {
                        var sourceBreakPoint = sourceBreakpoints[j];
                        var sequencePoint = Array.Find(sequencePoints, sp => sp.Start.line == sourceBreakPoint.Line);

                        if (sequencePoint != null)
                        {
                            breakpoints.Add(sequencePoint.Address);

                            yield return new Breakpoint()
                            {
                                Verified = true,
                                Column = sequencePoint.Start.column,
                                EndColumn = sequencePoint.End.column,
                                Line = sequencePoint.Start.line,
                                EndLine = sequencePoint.End.line,
                                Source = source

                            };
                        }
                        else
                        {
                            yield return new Breakpoint()
                            {
                                Verified = false,
                                Column = sourceBreakPoint.Column,
                                Line = sourceBreakPoint.Line,
                                Source = source
                            };
                        }
                    }

                    this.breakpoints[(source.Path, contract.ScriptHash)] = breakpoints.ToImmutableHashSet();
                }
            }
        }

        const VMState HALT_OR_FAULT = VMState.HALT | VMState.FAULT;

        bool CheckBreakpoint(UInt160 scriptHash, int instructionPointer)
        {
            foreach (var kvp in breakpoints)
            {
                if (kvp.Key.scriptHash.Equals(scriptHash))
                {
                    return kvp.Value.Contains(instructionPointer);
                }
            }

            return false;
        }

        bool CheckBreakpoint()
        {
            if ((engine.State & HALT_OR_FAULT) == 0)
            {
                var context = engine.CurrentContext;
                var scriptHash = new UInt160(context.ScriptHash);

                return CheckBreakpoint(scriptHash, context.InstructionPointer);
            }

            return false;
        }

        private void FireStoppedEvent(StoppedEvent.ReasonValue reasonValue)
        {
            ClearVariableContainers();

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
                foreach (var result in GetResults())
                {
                    sendEvent(new OutputEvent()
                    {
                        Category = OutputEvent.CategoryValue.Stdout,
                        Output = $"Return: {result}\n",
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

        public void Continue()
        {
            while ((engine.State & HALT_OR_FAULT) == 0)
            {
                engine.ExecuteInstruction();

                if (CheckBreakpoint())
                {
                    break;
                }
            }

            FireStoppedEvent(StoppedEvent.ReasonValue.Breakpoint);
        }

        void Step(Func<int, int, bool> compare)
        {
            var c = engine.InvocationStack.Count;
            var stopReason = StoppedEvent.ReasonValue.Step;
            while ((engine.State & HALT_OR_FAULT) == 0)
            {
                engine.ExecuteInstruction();

                if ((engine.State & HALT_OR_FAULT) != 0)
                {
                    break;
                }

                if (disassemblyView)
                {
                    break;
                }

                if (CheckBreakpoint())
                {
                    stopReason = StoppedEvent.ReasonValue.Breakpoint;
                    break;
                }

                var contract = GetContract(engine.CurrentContext);
                if (compare(engine.InvocationStack.Count, c)
                    && (contract?.CheckSequencePoint(engine.CurrentContext) ?? false))
                {
                    break;
                }
            }

            FireStoppedEvent(stopReason);
        }

        public void StepOver()
        {
            Step((currentStackSize, originalStackSize) => currentStackSize <= originalStackSize);
        }

        public void StepIn()
        {
            Step((_, __) => true);
        }

        public void StepOut()
        {
            Step((currentStackSize, originalStackSize) => currentStackSize < originalStackSize);
        }

        public SourceResponse GetSource(SourceArguments arguments)
        {
            var source = disassemblyManager.GetSource(arguments.SourceReference);
            return new SourceResponse(source)
            {
                MimeType = "text/x-neovm.disassembly"
            };
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
                var start = args.StartFrame ?? 0;
                var count = args.Levels ?? int.MaxValue;
                var end = Math.Min(engine.InvocationStack.Count, start + count);

                for (var i = start; i < end; i++)
                {
                    var context = engine.InvocationStack.Peek(i);
                    var contract = GetContract(context);
                    var method = contract?.GetMethod(context);

                    var frame = new StackFrame()
                    {
                        Id = i,
                        Name = method?.Name ?? $"frame {engine.InvocationStack.Count - i}",
                    };

                    if (disassemblyView)
                    {
                        var (source, line) = disassemblyManager.GetSource(context.ScriptHash, context.InstructionPointer);
                        frame.Source = source;
                        frame.Line = line;
                    }
                    else
                    {
                        var sequencePoint = method?.GetCurrentSequencePoint(context);

                        if (sequencePoint != null)
                        {
                            frame.Source = new Source()
                            {
                                Name = Path.GetFileName(sequencePoint.Document),
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

        void ClearVariableContainers()
        {
            variableContainers.Clear();
        }

        public int AddVariableContainer(IVariableContainer container)
        {
            var id = container.GetHashCode();
            if (variableContainers.TryAdd(id, container))
            {
                return id;
            }

            throw new Exception();
        }

        public IEnumerable<Scope> GetScopes(ScopesArguments args)
        {
            if ((engine.State & HALT_OR_FAULT) == 0)
            {
                var context = engine.InvocationStack.Peek(args.FrameId);
                var contract = GetContract(context);

                int contextID = 0;
                if (contract != null)
                {
                    contextID = AddVariableContainer(
                        new ExecutionContextContainer(this, context, contract));
                }
                var evalStackID = AddVariableContainer(
                    new ExecutionStackContainer(this, context.EvaluationStack, "evalStack"));
                var altStackID = AddVariableContainer(
                    new ExecutionStackContainer(this, context.AltStack, "altStack"));
                var storageID = AddVariableContainer(
                    engine.GetStorageContainer(this, context.ScriptHash));

                if (disassemblyView)
                {
                    yield return new Scope("Evaluation Stack", evalStackID, false);
                    yield return new Scope("Alt Stack", altStackID, false);
                }
                else
                {
                    if (contract != null)
                    {
                        yield return new Scope("Locals", contextID, false);
                    }
                }

                yield return new Scope("Storage", storageID, false);
            }
        }

        public IEnumerable<Variable> GetVariables(VariablesArguments args)
        {
            if ((engine.State & HALT_OR_FAULT) == 0)
            {
                if (variableContainers.TryGetValue(args.VariablesReference, out var container))
                {
                    return container.GetVariables();
                }
            }

            return Enumerable.Empty<Variable>();
        }

        private string GetResult(NeoArrayContainer container)
        {
            var array = new Newtonsoft.Json.Linq.JArray();
            foreach (var x in container.GetVariables())
            {
                array.Add(GetResult(x));
            }
            return array.ToString(Newtonsoft.Json.Formatting.Indented);
        }

        private string GetResult(ByteArrayContainer container)
        {
            return container.Span.ToHexString();
        }

        private string GetResult(Variable variable)
        {
            if (variable.VariablesReference == 0)
            {
                return variable.Value;
            }

            if (variableContainers.TryGetValue(variable.VariablesReference, out var container))
            {
                switch (container)
                {
                    case NeoArrayContainer arrayContainer:
                        return GetResult(arrayContainer);
                    case ByteArrayContainer byteArrayContainer:
                        return GetResult(byteArrayContainer);
                    default:
                        return $"{container.GetType().Name} unsupported container";
                }
            }

            return string.Empty;
        }

        string GetResult(StackItem item, string? typeHint = null)
        {
            if (typeHint == "ByteArray")
            {
                return item.GetByteArray().ToHexString();
            }

            return GetResult(item.GetVariable(this, string.Empty, typeHint));
        }

        IEnumerable<string> GetResults()
        {
            foreach (var (item, index) in engine.ResultStack.Select((_item, index) => (_item, index)))
            {
                var returnType = index < returnTypes.Count
                    ? returnTypes[index] : null;
                yield return GetResult(item, returnType);
            }
        }

        public EvaluateResponse Evaluate(EvaluateArguments args)
        {
            if ((engine.State & HALT_OR_FAULT) != 0)
                return DebugAdapter.FailedEvaluation;

            var (typeHint, index, variableName) = ParseEvalExpression(args.Expression);

            if (variableName.StartsWith("$storage"))
            {
                return engine.EvaluateStorageExpression(this, engine.CurrentContext.ScriptHash, args);
            }

            EvaluateResponse GetStackVariable(RandomAccessStack<StackItem> stack)
            {
                if (index.HasValue && (index.Value < stack.Count))
                {
                    var item = stack.Peek((int)index.Value);
                    var variable = item.GetVariable(this, "ZZZ", typeHint);
                    if (variable != null)
                    {
                        return new EvaluateResponse()
                        {
                            Result = variable.Value,
                            VariablesReference = variable.VariablesReference,
                            Type = variable.Type
                        };
                    }
                }
                return DebugAdapter.FailedEvaluation;
            }

            if (variableName.StartsWith("$evalStack"))
            {
                return GetStackVariable(engine.CurrentContext.EvaluationStack);
            }

            if (variableName.StartsWith("$altStack"))
            {
                return GetStackVariable(engine.CurrentContext.AltStack);
            }

            for (var stackIndex = 0; stackIndex < engine.InvocationStack.Count; stackIndex++)
            {
                var context = engine.InvocationStack.Peek(stackIndex);
                if (context.AltStack.Count <= 0)
                    continue;

                var contract = GetContract(context);
                var method = contract?.GetMethod(context);
                if (method == null)
                    continue;

                var locals = method.GetLocals().ToArray();
                var variables = (Neo.VM.Types.Array)context.AltStack.Peek(0);

                for (int varIndex = 0; varIndex < Math.Min(variables.Count, locals.Length); varIndex++)
                {
                    var local = locals[varIndex];
                    if (local.name == variableName)
                    {
                        var variable = GetVariable(variables[varIndex], local);
                        if (variable != null)
                        {
                            return new EvaluateResponse()
                            {
                                Result = variable.Value,
                                VariablesReference = variable.VariablesReference,
                                Type = variable.Type
                            };
                        }
                    }
                }
            }

            return DebugAdapter.FailedEvaluation;

            Variable? GetVariable(StackItem item, (string name, string type) local)
            {
                if (index.HasValue)
                {
                    if (item is Neo.VM.Types.Array neoArray
                        && index.Value < neoArray.Count)
                    {
                        return neoArray[(int)index.Value].GetVariable(this, local.name + $"[{index.Value}]", typeHint);
                    }
                }
                else
                {
                    return item.GetVariable(this, local.name, typeHint ?? local.type);
                }

                return null;
            }
        }
        private static readonly Regex indexRegex = new Regex(@"\[(\d+)\]$");

        public static readonly IReadOnlyDictionary<string, string> CastOperations = new Dictionary<string, string>()
            {
                { "int", "Integer" },
                { "bool", "Boolean" },
                { "string", "String" },
                { "hex", "HexString" },
                { "byte[]", "ByteArray" },
            }.ToImmutableDictionary();

        public static (string? typeHint, uint? index, string name) ParseEvalExpression(string expression)
        {
            static (string? typeHint, string text) ParsePrefix(string input)
            {
                foreach (var kvp in CastOperations)
                {
                    if (input.Length > kvp.Key.Length + 2
                        && input[0] == '('
                        && input.AsSpan().Slice(1, kvp.Key.Length).SequenceEqual(kvp.Key)
                        && input[kvp.Key.Length + 1] == ')')
                    {
                        return (kvp.Value, input.Substring(kvp.Key.Length + 2));
                    }
                }

                return (null, input);
            }

            static (uint? index, string text) ParseSuffix(string input)
            {
                var match = indexRegex.Match(input);
                if (match.Success)
                {
                    var matchValue = match.Groups[0].Value;
                    var indexValue = match.Groups[1].Value;
                    if (uint.TryParse(indexValue, out var index)
                        && index < int.MaxValue)
                    {
                        return (index, input.Substring(0, input.Length - matchValue.Length));
                    }
                }
                return (null, input);
            }

            var prefix = ParsePrefix(expression);
            var suffix = ParseSuffix(prefix.text);

            return (prefix.typeHint, suffix.index, suffix.text.Trim());
        }
    }
}
