using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo.VM;
using NeoDebug.Models;
using NeoDebug.VariableContainers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;

namespace NeoDebug
{
    internal class DebugSession : IVariableContainerSession
    {
        private readonly IExecutionEngine engine;
        private readonly Dictionary<int, HashSet<int>> breakPoints = new Dictionary<int, HashSet<int>>();
        private readonly Dictionary<int, IVariableContainer> variableContainers = new Dictionary<int, IVariableContainer>();

        public Contract Contract { get; }
        public Method Method { get; }

        public VMState EngineState => engine.State;

        public DebugSession(IExecutionEngine engine, Contract contract, Method method, ContractArgument[] arguments)
        {
            this.engine = engine;
            Contract = contract;
            Method = method;

            using (var builder = contract.BuildInvokeScript(arguments))
            {
                engine.LoadScript(builder.ToArray());
            }
        }

        public IEnumerable<Breakpoint> SetBreakpoints(Source source, IReadOnlyList<SourceBreakpoint> sourceBreakpoints)
        {
            var sourcePath = Path.GetFullPath(source.Path).ToLowerInvariant();
            var sourcePathHash = sourcePath.GetHashCode();

            breakPoints[sourcePathHash] = new HashSet<int>();

            if (sourceBreakpoints.Count == 0)
            {
                yield break;
            }

            var sequencePoints = Contract.DebugInfo.Methods
                .SelectMany(m => m.SequencePoints)
                .Where(sp => sourcePath.Equals(Path.GetFullPath(sp.Document), StringComparison.InvariantCultureIgnoreCase))
                .ToArray();

            foreach (var sourceBreakPoint in sourceBreakpoints)
            {
                var sequencePoint = Array.Find(sequencePoints, sp => sp.StartLine == sourceBreakPoint.Line);

                if (sequencePoint != null)
                {
                    breakPoints[sourcePathHash].Add(sequencePoint.Address);

                    yield return new Breakpoint()
                    {
                        Verified = true,
                        Column = sequencePoint.StartColumn,
                        EndColumn = sequencePoint.EndColumn,
                        Line = sequencePoint.StartLine,
                        EndLine = sequencePoint.EndLine,
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

                if (Contract.ScriptHash.AsSpan().SequenceEqual(context.ScriptHash))
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

        public void Continue()
        {
            while ((engine.State & HALT_OR_FAULT) == 0)
            {
                engine.ExecuteNext();

                if (CheckBreakpoint())
                {
                    break;
                }
            }
        }

        void Step(Func<int, int, bool> compare)
        {
            int c = engine.InvocationStack.Count;
            while ((engine.State & HALT_OR_FAULT) == 0)
            {
                engine.ExecuteNext();

                if ((engine.State & HALT_OR_FAULT) != 0)
                {
                    break;
                }

                if (CheckBreakpoint())
                {
                    break;
                }

                if (compare(engine.InvocationStack.Count, c) && Contract.CheckSequencePoint(engine.CurrentContext))
                {
                    break;
                }
            }
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
                    var method = Contract.GetMethod(context);

                    var frame = new StackFrame()
                    {
                        Id = i,
                        Name = method?.DisplayName ?? "<unknown>",
                        ModuleId = context.ScriptHash,
                    };

                    var sequencePoint = method?.GetCurrentSequencePoint(context);

                    if (sequencePoint != null)
                    {
                        frame.Source = new Source()
                        {
                            Name = Path.GetFileName(sequencePoint.Document),
                            Path = sequencePoint.Document
                        };
                        frame.Line = sequencePoint.StartLine;
                        frame.Column = sequencePoint.StartColumn;
                        frame.EndLine = sequencePoint.EndLine;
                        frame.EndColumn = sequencePoint.EndColumn;
                    }

                    yield return frame;
                }
            }
        }

        public void ClearVariableContainers()
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
                    new ExecutionContextContainer(this, context, Contract));
                yield return new Scope("Locals", contextID, false);

                var storageID = AddVariableContainer(engine.GetStorageContainer(this));
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
            return "0x" + container.AsBigInteger().ToString("x");
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

        private string GetResult(StackItem item, string? typeHint = null)
        {
            if (typeHint == "ByteArray")
            {
                return "0x" + new BigInteger(item.GetByteArray()).ToString("x");
            }

            return GetResult(item.GetVariable(this, string.Empty, typeHint));
        }

        public IEnumerable<string> GetResults()
        {
            var head = engine.ResultStack.FirstOrDefault();
            if (head != null)
            {
                yield return GetResult(head, Method.ReturnType);
            }
            foreach (var item in engine.ResultStack.Skip(1))
            {
                yield return GetResult(item);
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

            Variable? GetVariable(StackItem item, Parameter local)
            {
                if (index.HasValue)
                {
                    if (item is Neo.VM.Types.Array neoArray
                        && index.Value < neoArray.Count)
                    {
                        return neoArray[index.Value].GetVariable(this, local.Name + $"[{index.Value}]", typeHint);
                    }
                }
                else
                {
                    return item.GetVariable(this, local.Name, typeHint ?? local.Type);
                }

                return null;
            }

            for (var stackIndex = 0; stackIndex < engine.InvocationStack.Count; stackIndex++)
            {
                var context = engine.InvocationStack.Peek(stackIndex);
                if (context.AltStack.Count <= 0)
                    continue;

                var method = Contract.GetMethod(context);
                if (method == null)
                    continue;

                var locals = method.Locals.ToArray();
                var variables = (Neo.VM.Types.Array)context.AltStack.Peek(0);

                for (int varIndex = 0; varIndex < Math.Min(variables.Count, locals.Length); varIndex++)
                {
                    var local = locals[varIndex];
                    if (local.Name == variableName)
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
        }
    }
}
