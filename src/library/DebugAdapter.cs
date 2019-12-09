using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo.VM;
using NeoDebug.Models;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;

namespace NeoDebug
{
    public class DebugAdapter : DebugAdapterBase
    {
        private readonly Action<LogCategory, string> logger;
        private readonly Func<Contract, LaunchArguments, Action<OutputEvent>, IExecutionEngine> createEngineFunc;
        private readonly Func<byte[], byte[]> scriptHashFunc;
        private DebugSession? session;

        public DebugAdapter(Stream @in, Stream @out, Func<Contract, LaunchArguments, Action<OutputEvent>, IExecutionEngine> createEngineFunc,
                            Func<byte[], byte[]> scriptHashFunc, Action<LogCategory, string>? logger = null)
        {
            this.createEngineFunc = createEngineFunc;
            this.scriptHashFunc = scriptHashFunc;
            this.logger = logger ?? ((_, __) => { });

            InitializeProtocolClient(@in, @out);
            Protocol.LogMessage += (sender, args) => this.logger(args.Category, args.Message);
        }

        public void Run()
        {
            Protocol.Run();
        }

        void Log(string message, LogCategory category = LogCategory.DebugAdapterOutput)
        {
            logger(category, message);
        }

        protected override InitializeResponse HandleInitializeRequest(InitializeArguments arguments)
        {
            this.Protocol.SendEvent(new InitializedEvent());

            return new InitializeResponse()
            {
                SupportsConfigurationDoneRequest = true,
                SupportsEvaluateForHovers = true,
            };
        }

        protected override ConfigurationDoneResponse HandleConfigurationDoneRequest(ConfigurationDoneArguments arguments)
        {
            return new ConfigurationDoneResponse();
        }

        private static ContractParameterType ParseTypeName(string typeName)
        {
            return typeName switch
            {
                "Integer" => ContractParameterType.Integer,
                "String" => ContractParameterType.String,
                "Array" => ContractParameterType.Array,
                "Boolean" => ContractParameterType.Boolean,
                "ByteArray" => ContractParameterType.ByteArray,
                _ => throw new NotImplementedException(),
            };
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
                    }
                    break;
            }
            throw new NotImplementedException($"DebugAdapter.ConvertArgument {paramType} {arg}");
        }

        private static ContractArgument ConvertArgument((string name, string type) param, JToken? arg)
        {
            var type = ParseTypeName(param.type);
            return new ContractArgument(type, ConvertArgumentToObject(type, arg));
        }

        protected override LaunchResponse HandleLaunchRequest(LaunchArguments arguments)
        {
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

            try
            {
                var programFileName = arguments.ConfigurationProperties["program"].Value<string>();
                var contract = Contract.Load(programFileName, scriptHashFunc);
                var contractArgs = GetArguments(contract.EntryPoint).ToArray();
                var returnTypes = GetReturnTypes().ToArray();

                var engine = createEngineFunc(contract, arguments, outputEvent => Protocol.SendEvent(outputEvent));
                session = new DebugSession(engine, contract, contractArgs, returnTypes);

                session.StepIn();
                Protocol.SendEvent(new StoppedEvent(StoppedEvent.ReasonValue.Entry) { ThreadId = 1 });

                return new LaunchResponse();
            }
            catch (Exception ex)
            {
                Log(ex.Message, LogCategory.DebugAdapterOutput);
                throw new ProtocolException(ex.Message, ex);
            }
        }

        protected override DisconnectResponse HandleDisconnectRequest(DisconnectArguments arguments)
        {
            return new DisconnectResponse();
        }

        protected override SetExceptionBreakpointsResponse HandleSetExceptionBreakpointsRequest(SetExceptionBreakpointsArguments arguments)
        {
            return new SetExceptionBreakpointsResponse();
        }

        protected override ThreadsResponse HandleThreadsRequest(ThreadsArguments arguments)
        {
            try
            {
                if (session == null) throw new InvalidOperationException();

                var threads = session.GetThreads().ToList();
                return new ThreadsResponse(threads);
            }
            catch (Exception ex)
            {
                Log(ex.Message, LogCategory.DebugAdapterOutput);
                throw new ProtocolException(ex.Message, ex);
            }
        }

        protected override StackTraceResponse HandleStackTraceRequest(StackTraceArguments arguments)
        {
            try
            {
                if (session == null) throw new InvalidOperationException();

                var frames = session.GetStackFrames(arguments).ToList();
                return new StackTraceResponse(frames);
            }
            catch (Exception ex)
            {
                Log(ex.Message, LogCategory.DebugAdapterOutput);
                throw new ProtocolException(ex.Message, ex);
            }
        }

        protected override ScopesResponse HandleScopesRequest(ScopesArguments arguments)
        {
            try
            {
                if (session == null) throw new InvalidOperationException();

                var scopes = session.GetScopes(arguments).ToList();
                return new ScopesResponse(scopes);
            }
            catch (Exception ex)
            {
                Log(ex.Message, LogCategory.DebugAdapterOutput);
                throw new ProtocolException(ex.Message, ex);
            }
        }

        protected override VariablesResponse HandleVariablesRequest(VariablesArguments arguments)
        {
            try
            {
                if (session == null) throw new InvalidOperationException();

                var variables = session.GetVariables(arguments).ToList();
                return new VariablesResponse(variables);
            }
            catch (Exception ex)
            {
                Log(ex.Message, LogCategory.DebugAdapterOutput);
                throw new ProtocolException(ex.Message, ex);
            }
        }

        public static readonly EvaluateResponse FailedEvaluation = new EvaluateResponse()
        {
            PresentationHint = new VariablePresentationHint()
            {
                Attributes = VariablePresentationHint.AttributesValue.FailedEvaluation
            }
        };

        protected override EvaluateResponse HandleEvaluateRequest(EvaluateArguments arguments)
        {
            try
            {
                if (session == null) throw new InvalidOperationException();

                return session.Evaluate(arguments);
            }
            catch (Exception)
            {
                return FailedEvaluation;
            }
        }

        private void FireStoppedEvent(StoppedEvent.ReasonValue reasonValue)
        {
            try
            {
                if (session == null) throw new InvalidOperationException();

                session.ClearVariableContainers();

                if ((session.EngineState & VMState.FAULT) != 0)
                {
                    Protocol.SendEvent(new OutputEvent()
                    {
                        Category = OutputEvent.CategoryValue.Stderr,
                        Output = "Engine State Faulted\n",
                    });
                    Protocol.SendEvent(new TerminatedEvent());
                }
                if ((session.EngineState & VMState.HALT) != 0)
                {
                    foreach (var result in session.GetResults())
                    {
                        Protocol.SendEvent(new OutputEvent()
                        {
                            Category = OutputEvent.CategoryValue.Stdout,
                            Output = $"Return: {result}\n",
                        });
                    }
                    Protocol.SendEvent(new ExitedEvent());
                    Protocol.SendEvent(new TerminatedEvent());
                }
                else
                {
                    Protocol.SendEvent(new StoppedEvent(reasonValue) { ThreadId = 1 });
                }
            }
            catch (Exception ex)
            {
                Log(ex.Message, LogCategory.DebugAdapterOutput);
                throw new ProtocolException(ex.Message, ex);
            }
        }

        protected override ContinueResponse HandleContinueRequest(ContinueArguments arguments)
        {
            try
            {
                if (session == null) throw new InvalidOperationException();

                session.Continue();
                FireStoppedEvent(StoppedEvent.ReasonValue.Step);

                return new ContinueResponse();
            }
            catch (Exception ex)
            {
                Log(ex.Message, LogCategory.DebugAdapterOutput);
                throw new ProtocolException(ex.Message, ex);
            }
        }

        protected override StepInResponse HandleStepInRequest(StepInArguments arguments)
        {
            try
            {
                if (session == null) throw new InvalidOperationException();

                session.StepIn();
                FireStoppedEvent(StoppedEvent.ReasonValue.Step);

                return new StepInResponse();
            }
            catch (Exception ex)
            {
                Log(ex.Message, LogCategory.DebugAdapterOutput);
                throw new ProtocolException(ex.Message, ex);
            }
        }

        protected override StepOutResponse HandleStepOutRequest(StepOutArguments arguments)
        {
            try
            {
                if (session == null) throw new InvalidOperationException();

                session.StepOut();
                FireStoppedEvent(StoppedEvent.ReasonValue.Step);

                return new StepOutResponse();
            }
            catch (Exception ex)
            {
                Log(ex.Message, LogCategory.DebugAdapterOutput);
                throw new ProtocolException(ex.Message, ex);
            }
        }

        // Next == StepOver in VSCode UI
        protected override NextResponse HandleNextRequest(NextArguments arguments)
        {
            try
            {
                if (session == null) throw new InvalidOperationException();

                session.StepOver();
                FireStoppedEvent(StoppedEvent.ReasonValue.Step);

                return new NextResponse();
            }
            catch (Exception ex)
            {
                Log(ex.Message, LogCategory.DebugAdapterOutput);
                throw new ProtocolException(ex.Message, ex);
            }
        }

        protected override SetBreakpointsResponse HandleSetBreakpointsRequest(SetBreakpointsArguments arguments)
        {
            try
            {
                if (session == null) throw new InvalidOperationException();

                var breakpoints = session.SetBreakpoints(arguments.Source, arguments.Breakpoints).ToList();
                return new SetBreakpointsResponse(breakpoints);
            }
            catch (Exception ex)
            {
                Log(ex.Message, LogCategory.DebugAdapterOutput);
                throw new ProtocolException(ex.Message, ex);
            }
        }
    }
}
