using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo.VM;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;

namespace Neo.DebugAdapter
{
    class NeoDebugAdapter : DebugAdapterBase
    {
        Action<LogCategory, string> logger;

        public NeoDebugAdapter(Stream @in, Stream @out, Action<LogCategory, string> logger = null)
        {
            Action<LogCategory, string> nullLogger = (__, _) => { };
            this.logger = logger ?? nullLogger;

            InitializeProtocolClient(@in, @out);
        }

        public void Run()
        {
            Protocol.Run();
        }

        void Log(string message, LogCategory cat = LogCategory.DebugAdapterOutput)
        {
            logger(cat, message);
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

        NeoDebugSession session;

        static ContractParameterType ConvertTypeName(string typeName)
        {
            switch (typeName)
            {
                case "Integer":
                    return ContractParameterType.Integer;
                case "String":
                    return ContractParameterType.String;
                case "Array":
                    return ContractParameterType.Array;
                default:
                    throw new NotImplementedException();
            }
        }

        static object ConvertArg(JToken arg)
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
                    return new ContractArgument
                    {
                        Type = ContractParameterType.String,
                        Value = arg.Value<string>(),
                    };
                default:
                    throw new NotImplementedException($"ConvertArg {arg.Type}");
            }
        }

        static object ConvertArg(JToken arg, ContractParameterType paramType)
        {
            switch (paramType)
            {
                case ContractParameterType.Boolean:
                    return arg.Type == JTokenType.Boolean
                        ? arg.Value<bool>()
                        : bool.Parse(arg.ToString());
                case ContractParameterType.Integer:
                    return arg.Type == JTokenType.Integer
                        ? new BigInteger(arg.Value<int>())
                        : BigInteger.Parse(arg.ToString());
                case ContractParameterType.String:
                    return arg.ToString();
                case ContractParameterType.Array:
                    return arg.Select(ConvertArg).ToArray();
                default:
                    throw new NotImplementedException($"ConvertArg {paramType}");
            }
        }

        static ContractArgument ConvertArg(JToken arg, Parameter param)
        {
            var type = ConvertTypeName(param.Type);
            return new ContractArgument()
            {
                Type = type,
                Value = ConvertArg(arg, type)
            };
        }

        protected override LaunchResponse HandleLaunchRequest(LaunchArguments arguments)
        {
            var programFileName = (string)arguments.ConfigurationProperties["program"];
            var contract = Contract.Load(programFileName);

            var entrypoint = contract.GetEntryPoint();

            var args = arguments.ConfigurationProperties["args"]
                .Zip(entrypoint.Parameters, ConvertArg);

            session = new NeoDebugSession(contract, args);
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

        private void FireEvents(StoppedEvent.ReasonValue reasonValue)
        {
            session.ClearVariableContainers();

            if ((session.EngineState & VMState.FAULT) == 0)
            {
                // there's been a fault;
            }
            if ((session.EngineState & VMState.HALT) != 0)
            {
                var entryPoint = session.Contract.GetEntryPoint();

                foreach (var item in session.GetResults())
                {
                    Protocol.SendEvent(new OutputEvent(item.GetStackItemValue(entryPoint.ReturnType)));
                }
                Protocol.SendEvent(new TerminatedEvent());
            }
            else
            {
                Protocol.SendEvent(new StoppedEvent(reasonValue) { ThreadId = 1 });
            }
        }

        // https://microsoft.github.io/debug-adapter-protocol/specification#Requests_Continue
        protected override ContinueResponse HandleContinueRequest(ContinueArguments arguments)
        {
            session.Continue();
            FireEvents(StoppedEvent.ReasonValue.Step);

            return new ContinueResponse();
        }

        // https://microsoft.github.io/debug-adapter-protocol/specification#Requests_StepIn
        protected override StepInResponse HandleStepInRequest(StepInArguments arguments)
        {
            session.StepIn();
            FireEvents(StoppedEvent.ReasonValue.Step);

            return new StepInResponse();
        }

        // https://microsoft.github.io/debug-adapter-protocol/specification#Requests_StepOut
        protected override StepOutResponse HandleStepOutRequest(StepOutArguments arguments)
        {
            session.StepOut();
            FireEvents(StoppedEvent.ReasonValue.Step);

            return new StepOutResponse();
        }

        // Next == StepOver in VSCode UI
        // https://microsoft.github.io/debug-adapter-protocol/specification#Requests_Next
        protected override NextResponse HandleNextRequest(NextArguments arguments)
        {
            session.StepOver();
            FireEvents(StoppedEvent.ReasonValue.Step);

            return new NextResponse();
        }

        protected override SetBreakpointsResponse HandleSetBreakpointsRequest(SetBreakpointsArguments arguments)
        {
            return new SetBreakpointsResponse();
        }
    }
}
