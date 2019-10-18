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
            switch (typeName)
            {
                case "Integer":
                    return ContractParameterType.Integer;
                case "String":
                    return ContractParameterType.String;
                case "Array":
                    return ContractParameterType.Array;
                case "Boolean":
                    return ContractParameterType.Boolean;
                default:
                    throw new NotImplementedException();
            }
        }

        private static object ConvertArgument(JToken arg)
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

        private static object ConvertArgument(ContractParameterType paramType, JToken? arg)
        {
            switch (paramType)
            {
                case ContractParameterType.Boolean:
                    return arg?.Type == JTokenType.Boolean
                        ? arg.Value<bool>()
                        : bool.Parse(arg!.ToString());
                case ContractParameterType.Integer:
                    return arg?.Type == JTokenType.Integer
                        ? new BigInteger(arg.Value<int>())
                        : BigInteger.Parse(arg!.ToString());
                case ContractParameterType.String:
                    return arg?.ToString() ?? "";
                case ContractParameterType.Array:
                    return arg?.Select(ConvertArgument).ToArray() ?? Array.Empty<object>();
                default:
                    throw new NotImplementedException($"DebugAdapter.ConvertArgument {paramType} {arg}");
            }
        }

        private static ContractArgument ConvertArgument(Parameter param, JToken? arg)
        {
            var type = ParseTypeName(param.Type);
            return new ContractArgument(type, ConvertArgument(type, arg));
        }

        protected override LaunchResponse HandleLaunchRequest(LaunchArguments arguments)
        {
            IEnumerable<ContractArgument> GetArguments(Method method)
            {
                if (!arguments.ConfigurationProperties.TryGetValue("args", out var args))
                {
                    // initialize args to empty JArray
                    args = new JArray();
                }

                if (args.Type != JTokenType.Array)
                {
                    args = new JArray(args);
                }

                for (int i = 0; i < method.Parameters.Count; i++)
                {
                    var param = method.Parameters[i];
                    JToken? arg = args.ElementAtOrDefault(i);
                    yield return ConvertArgument(param, arg);
                }
            }

            try
            {
                var programFileName = (string)arguments.ConfigurationProperties["program"];
                var contract = Contract.Load(programFileName, scriptHashFunc);
                var contractArgs = GetArguments(contract.EntryPoint);
                var engine = createEngineFunc(contract, arguments, outputEvent => Protocol.SendEvent(outputEvent));

                session = new DebugSession(engine, contract, contractArgs.ToArray());

                session.StepIn();
                Protocol.SendEvent(new StoppedEvent(StoppedEvent.ReasonValue.Entry) { ThreadId = 1 });

                return new LaunchResponse();
            }
            catch (Exception ex)
            {
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
                throw new ProtocolException(ex.Message, ex);
            }
        }

        protected override EvaluateResponse HandleEvaluateRequest(EvaluateArguments arguments)
        {
            try
            {
                if (session == null) throw new InvalidOperationException();

                return session.Evaluate(arguments);
            }
            catch (Exception ex)
            {
                throw new ProtocolException(ex.Message, ex);
            }
        }

        private void FireStoppedEvent(StoppedEvent.ReasonValue reasonValue)
        {
            try
            {
                if (session == null) throw new InvalidOperationException();

                static string GetResult(StackItem item, string type)
                {
                    if (type == "ByteArray" || type == string.Empty)
                    {
                        return $@"byte[{item.GetByteArray().Length}]
as string:  ""{item.GetString()}""
as boolean: {item.GetBoolean()}
as integer: {item.GetBigInteger()}
as hex:     0x{item.GetBigInteger().ToString("x")}";
                    }

                    if (item.TryGetValue(type, out var value))
                    {
                        return value;
                    }

                    throw new Exception($"couldn't convert {type}");
                }

                session.ClearVariableContainers();

                if ((session.EngineState & VMState.FAULT) != 0)
                {
                    throw new Exception("Engine State Faulted");
                }
                if ((session.EngineState & VMState.HALT) != 0)
                {
                    foreach (var item in session.GetResults())
                    {
                        Protocol.SendEvent(new OutputEvent(GetResult(item, session.Contract.EntryPoint.ReturnType)));
                    }
                    Protocol.SendEvent(new TerminatedEvent());
                }
                else
                {
                    Protocol.SendEvent(new StoppedEvent(reasonValue) { ThreadId = 1 });
                }
            }
            catch (Exception ex)
            {
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
                throw new ProtocolException(ex.Message, ex);
            }
        }
    }
}
