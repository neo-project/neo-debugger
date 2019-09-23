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
        private readonly Func<Contract, LaunchArguments, IExecutionEngine> createEngineFunc;
        private readonly Func<byte[], byte[]> scriptHashFunc;
        private DebugSession session;

        public DebugAdapter(Stream @in, Stream @out, Func<Contract, LaunchArguments, IExecutionEngine> createEngineFunc,
                            Func<byte[], byte[]> scriptHashFunc, Action<LogCategory, string> logger = null)
        {
            this.createEngineFunc = createEngineFunc;
            this.scriptHashFunc = scriptHashFunc;
            this.logger = logger ?? ((_, __) => { });

            InitializeProtocolClient(@in, @out);
            Protocol.LogMessage += (sender, args) => logger(args.Category, args.Message);
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
                    return new ContractArgument
                    {
                        Type = ContractParameterType.Integer,
                        Value = new BigInteger(arg.Value<int>()),
                    };
                case JTokenType.String:
                    var value = arg.Value<string>();
                    if (value.TryParseBigInteger(out var bigInteger))
                    {
                        return new ContractArgument
                        {
                            Type = ContractParameterType.Integer,
                            Value = bigInteger,
                        };
                    }
                    else
                    {
                        return new ContractArgument
                        {
                            Type = ContractParameterType.String,
                            Value = value,
                        };
                    }
                default:
                    throw new NotImplementedException($"DebugAdapter.ConvertArgument {arg.Type}");
            }
        }

        private static object ConvertArgument(ContractParameterType paramType, JToken arg)
        {
            switch (paramType)
            {
                case ContractParameterType.Boolean:
                    return arg?.Type == JTokenType.Boolean
                        ? arg.Value<bool>()
                        : bool.Parse(arg.ToString());
                case ContractParameterType.Integer:
                    return arg?.Type == JTokenType.Integer
                        ? new BigInteger(arg.Value<int>())
                        : BigInteger.Parse(arg.ToString());
                case ContractParameterType.String:
                    return arg?.ToString() ?? "";
                case ContractParameterType.Array:
                    return arg?.Select(ConvertArgument).ToArray() ?? new object[0];
                default:
                    throw new NotImplementedException($"DebugAdapter.ConvertArgument {paramType} {arg}");
            }
        }

        private static ContractArgument ConvertArgument(JToken arg, Parameter param)
        {
            var type = ParseTypeName(param.Type);
            return new ContractArgument()
            {
                Type = type,
                Value = ConvertArgument(type, arg)
            };
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
                    var arg = args.ElementAtOrDefault(i);
                    yield return ConvertArgument(arg, param);
                }
            }

            var programFileName = (string)arguments.ConfigurationProperties["program"];
            var contract = Contract.Load(programFileName, scriptHashFunc);
            var contractArgs = GetArguments(contract.EntryPoint);
            var engine = createEngineFunc(contract, arguments);

            session = new DebugSession(engine, contract, contractArgs.ToArray());

            session.StepIn();
            Protocol.SendEvent(new StoppedEvent(StoppedEvent.ReasonValue.Entry) { ThreadId = 1 });

            return new LaunchResponse();
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
            var threads = session.GetThreads().ToList();
            return new ThreadsResponse(threads);
        }

        protected override StackTraceResponse HandleStackTraceRequest(StackTraceArguments arguments)
        {
            var frames = session.GetStackFrames(arguments).ToList();
            return new StackTraceResponse(frames);
        }

        protected override ScopesResponse HandleScopesRequest(ScopesArguments arguments)
        {
            var scopes = session.GetScopes(arguments).ToList();
            return new ScopesResponse(scopes);
        }

        protected override VariablesResponse HandleVariablesRequest(VariablesArguments arguments)
        {
            var variables = session.GetVariables(arguments).ToList();
            return new VariablesResponse(variables);
        }

        private void FireStoppedEvent(StoppedEvent.ReasonValue reasonValue)
        {
            string GetResult(StackItem item, string type)
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

            if ((session.EngineState & VMState.FAULT) == 0)
            {
                // there's been a fault;
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

        protected override ContinueResponse HandleContinueRequest(ContinueArguments arguments)
        {
            session.Continue();
            FireStoppedEvent(StoppedEvent.ReasonValue.Step);

            return new ContinueResponse();
        }

        protected override StepInResponse HandleStepInRequest(StepInArguments arguments)
        {
            session.StepIn();
            FireStoppedEvent(StoppedEvent.ReasonValue.Step);

            return new StepInResponse();
        }

        protected override StepOutResponse HandleStepOutRequest(StepOutArguments arguments)
        {
            session.StepOut();
            FireStoppedEvent(StoppedEvent.ReasonValue.Step);

            return new StepOutResponse();
        }

        // Next == StepOver in VSCode UI
        protected override NextResponse HandleNextRequest(NextArguments arguments)
        {
            session.StepOver();
            FireStoppedEvent(StoppedEvent.ReasonValue.Step);

            return new NextResponse();
        }

        protected override SetBreakpointsResponse HandleSetBreakpointsRequest(SetBreakpointsArguments arguments)
        {
            var breakpoints = session.SetBreakpoints(arguments.Source, arguments.Breakpoints).ToList();
            return new SetBreakpointsResponse(breakpoints);
        }
    }
}
