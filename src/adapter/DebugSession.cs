using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo.VM;
using NeoDebug.Models;
using NeoDebug.VariableContainers;
using NeoFx;
using Newtonsoft.Json.Linq;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;

namespace NeoDebug
{
    class DebugSession : IVariableContainerSession
    {
        class BreakpointManager
        {
            /*
            
            There are two ways to set breakpoints. Source and Disassembly
            For source, we get a source file + a 



             */

        }

        private readonly DebugExecutionEngine engine;
        private readonly Contract contract;
        private readonly Action<DebugEvent> sendEvent;
        private readonly ReadOnlyMemory<string> returnTypes;
        private readonly Dictionary<int, HashSet<int>> breakPoints = new Dictionary<int, HashSet<int>>();
        private readonly Dictionary<int, IVariableContainer> variableContainers = new Dictionary<int, IVariableContainer>();
        private readonly DisassemblyManager disassemblyManager;
        private bool disassemblyView = true;

        public DebugSession(DebugExecutionEngine engine, Contract contract, Action<DebugEvent> sendEvent, ContractArgument[] arguments, ReadOnlyMemory<string> returnTypes)
        {
            this.engine = engine;
            this.sendEvent = sendEvent;
            this.contract = contract;
            this.returnTypes = returnTypes;
            this.disassemblyManager = new DisassemblyManager(engine.GetMethodName);

            var invokeScript = contract.BuildInvokeScript(arguments);
            engine.LoadScript(invokeScript);

            disassemblyManager.Add(invokeScript);
            disassemblyManager.Add(contract.Script);

            if (!disassemblyView)
                StepIn();

            FireStoppedEvent(StoppedEvent.ReasonValue.Entry);
        }

        private static ContractArgument ConvertArgument(JToken arg)
        {
            switch (arg.Type)
            {
                case JTokenType.Integer:
                    return new ContractArgument(ContractParameterType.Integer, new BigInteger(arg.Value<int>()));
                case JTokenType.String:
                    var value = arg.Value<string>();
                    if (value.TryParseBigInteger(out var bigInteger))
                    {
                        return new ContractArgument(ContractParameterType.Integer, bigInteger);
                    }
                    else
                    {
                        return new ContractArgument(ContractParameterType.String, value);
                    }
                default:
                    throw new NotImplementedException($"DebugAdapter.ConvertArgument {arg.Type}");
            }
        }

        private static object ConvertArgumentToObject(ContractParameterType paramType, JToken? arg)
        {
            if (arg == null)
            {
                return paramType switch
                {
                    ContractParameterType.Boolean => false,
                    ContractParameterType.String => string.Empty,
                    ContractParameterType.Array => Array.Empty<ContractArgument>(),
                    _ => BigInteger.Zero,
                };
            }

            switch (paramType)
            {
                case ContractParameterType.Boolean:
                    return arg.Value<bool>();
                case ContractParameterType.Integer:
                    return arg.Type == JTokenType.Integer
                        ? new BigInteger(arg.Value<int>())
                        : BigInteger.Parse(arg.ToString());
                case ContractParameterType.String:
                    return arg.ToString();
                case ContractParameterType.Array:
                    return arg.Select(ConvertArgument).ToArray();
                case ContractParameterType.ByteArray:
                    {
                        var value = arg.ToString();
                        if (value.TryParseBigInteger(out var bigInteger))
                        {
                            return bigInteger;
                        }

                        var byteCount = Encoding.UTF8.GetByteCount(value);
                        using var owner = MemoryPool<byte>.Shared.Rent(byteCount);
                        var span = owner.Memory.Span.Slice(0, byteCount);
                        Encoding.UTF8.GetBytes(value, span);
                        return new BigInteger(span);
                    }
            }
            throw new NotImplementedException($"DebugAdapter.ConvertArgument {paramType} {arg}");
        }

        private static ContractArgument ConvertArgument((string name, string type) param, JToken? arg)
        {
            var type = param.type switch
            {
                "Integer" => ContractParameterType.Integer,
                "String" => ContractParameterType.String,
                "Array" => ContractParameterType.Array,
                "Boolean" => ContractParameterType.Boolean,
                "ByteArray" => ContractParameterType.ByteArray,
                "" => ContractParameterType.ByteArray,
                _ => throw new NotImplementedException(),
            };

            return new ContractArgument(type, ConvertArgumentToObject(type, arg));
        }

        static public DebugSession Create(Contract contract, LaunchArguments arguments, Action<DebugEvent> sendEvent)
        {
            var contractArgs = GetArguments(contract.EntryPoint).ToArray();
            var returnTypes = GetReturnTypes().ToArray();

            var engine = DebugExecutionEngine.Create(contract, arguments, outputEvent => sendEvent(outputEvent));
            return new DebugSession(engine, contract, sendEvent, contractArgs, returnTypes);

            JArray GetArgsConfig()
            {
                if (arguments.ConfigurationProperties.TryGetValue("args", out var args))
                {
                    if (args is JArray jArray)
                    {
                        return jArray;
                    }

                    return new JArray(args);
                }

                return new JArray();
            }

            IEnumerable<ContractArgument> GetArguments(MethodDebugInfo method)
            {
                var args = GetArgsConfig();
                for (int i = 0; i < method.Parameters.Count; i++)
                {
                    yield return ConvertArgument(
                        method.Parameters[i],
                        i < args.Count ? args[i] : null);
                }
            }

            IEnumerable<string> GetReturnTypes()
            {
                if (arguments.ConfigurationProperties.TryGetValue("return-types", out var returnTypes))
                {
                    foreach (var returnType in returnTypes)
                    {
                        yield return Helpers.CastOperations[returnType.Value<string>()];
                    }
                }
            }
        }

        public IEnumerable<Breakpoint> SetBreakpoints(Source source, IReadOnlyList<SourceBreakpoint> sourceBreakpoints)
        {
            if (UInt160.TryParse(source.Name, out var zzz))
            {

            }
            var sourcePath = Path.GetFullPath(source.Path).ToLowerInvariant();
            var sourcePathHash = sourcePath.GetHashCode();

            breakPoints[sourcePathHash] = new HashSet<int>();

            if (sourceBreakpoints.Count == 0)
            {
                yield break;
            }

            var sequencePoints = contract.DebugInfo.Methods
                .SelectMany(m => m.SequencePoints)
                .Where(sp => sourcePath.Equals(Path.GetFullPath(sp.Document), StringComparison.InvariantCultureIgnoreCase))
                .ToArray();

            foreach (var sourceBreakPoint in sourceBreakpoints)
            {
                var sequencePoint = Array.Find(sequencePoints, sp => sp.Start.line == sourceBreakPoint.Line);

                if (sequencePoint != null)
                {
                    breakPoints[sourcePathHash].Add(sequencePoint.Address);

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
        }


        const VMState HALT_OR_FAULT = VMState.HALT | VMState.FAULT;

        bool CheckBreakpoint()
        {
            if ((engine.State & HALT_OR_FAULT) == 0)
            {
                var context = engine.CurrentContext;
                var scriptHash = new UInt160(context.ScriptHash);

                if (contract.ScriptHash == scriptHash)
                {
                    var ip = context.InstructionPointer;
                    foreach (var kvp in breakPoints)
                    {
                        if (kvp.Value.Contains(ip))
                        {
                            return true;
                        }
                    }
                }
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

                if (compare(engine.InvocationStack.Count, c) && contract.CheckSequencePoint(engine.CurrentContext))
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
                    var method = contract.GetMethod(context);

                    var frame = new StackFrame()
                    {
                        Id = i,
                        Name = method?.Name ?? $"frame {engine.InvocationStack.Count - i}",
                    };

                    if (disassemblyView)
                    {
                        var (source, line) = disassemblyManager.GetSource(context);
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

                var contextID = AddVariableContainer(
                    new ExecutionContextContainer(this, context, contract));
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
                    yield return new Scope("Locals", contextID, false);
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
                return Helpers.ToHexString(item.GetByteArray());
            }

            return GetResult(item.GetVariable(this, string.Empty, typeHint));
        }

        IEnumerable<string> GetResults()
        {
            foreach (var (item, index) in engine.ResultStack.Select((_item, index) => (_item, index)))
            {
                var returnType = index < returnTypes.Length
                    ? returnTypes.Span[index] : null;
                yield return GetResult(item, returnType);
            }
        }

        public EvaluateResponse Evaluate(EvaluateArguments args)
        {
            if ((engine.State & HALT_OR_FAULT) != 0)
                return DebugAdapter.FailedEvaluation;

            var (typeHint, index, variableName) = Helpers.ParseEvalExpression(args.Expression);

            if (variableName.StartsWith("$storage"))
            {
                return engine.EvaluateStorageExpression(this, args);
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

                var method = contract.GetMethod(context);
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
    }
}
