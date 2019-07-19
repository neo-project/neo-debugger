using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo.VM;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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

        protected override LaunchResponse HandleLaunchRequest(LaunchArguments arguments)
        {
            var programFileName = (string)arguments.ConfigurationProperties["program"];
            var contract = Contract.Load(programFileName);

            var args = arguments.ConfigurationProperties["args"]
                .Select(j => j.Value<string>())
                .Zip(contract.AbiInfo.GetAbiEntryPoint().Parameters,
                    (a, p) => ContractArgument.FromArgument(p.Type, a));

            session = new NeoDebugSession(contract, args);

            var firstSequencePoint = contract.DebugInfo.GetEntryMethod().SequencePoints.FirstOrDefault();
            if (firstSequencePoint != null)
            {
                session.RunTo(contract.ScriptHash, firstSequencePoint);
            }
            else
            {
                // TODO: handle case where there is no debug info correctly
            }

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
            return new ThreadsResponse()
            {
                Threads = new List<Thread>() { new Thread(1, "thread 1") },
            };
        }

        protected override StackTraceResponse HandleStackTraceRequest(StackTraceArguments arguments)
        {
            var frames = session.GetStackFrames().ToList();
            return new StackTraceResponse(frames);
        }

        protected override ScopesResponse HandleScopesRequest(ScopesArguments arguments)
        {
            var scopes = session.GetScopes(arguments.FrameId).ToList();
            return new ScopesResponse(scopes);
        }

        protected override VariablesResponse HandleVariablesRequest(VariablesArguments arguments)
        {
            var variables = session.GetVariables(arguments.VariablesReference).ToList();
            return new VariablesResponse(variables);
        }

        private void FireEvents(StoppedEvent.ReasonValue reasonValue)
        {
            if ((session.EngineState & VMState.FAULT) == 0)
            {
                // there's been a fault;
            }
            if ((session.EngineState & VMState.HALT) != 0)
            {
                foreach (var item in session.GetResults())
                {
                    Protocol.SendEvent(new OutputEvent(item.GetResult()));
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
