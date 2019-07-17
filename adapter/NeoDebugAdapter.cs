using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo.VM;
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

            var args = contract.EntryPoint.ParseArguments(arguments.ConfigurationProperties["args"]);

            session = new NeoDebugSession(contract, args);
            session.Execute();

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
            if (session.Engine.State == VMState.BREAK)
            {
                var sp = session.Contract.SequencePoints
                    .SingleOrDefault(_sp => _sp.Address == session.Engine.CurrentContext.InstructionPointer); 

                if (sp != null)
                {
                    var stackFrame = new StackFrame()
                    {
                        Id = 0,
                        Name = "frame 0",
                        Source = new Source()
                        {
                            Name = Path.GetFileName(sp.Document),
                            Path = sp.Document
                        },
                        Line = sp.Start.line,
                        Column = sp.Start.column,
                        EndLine = sp.End.line,
                        EndColumn = sp.End.column,
                    };
                    return new StackTraceResponse(new List<StackFrame> { stackFrame });
                }
            }

            return new StackTraceResponse();
        }

        protected override ScopesResponse HandleScopesRequest(ScopesArguments arguments)
        {
            return new ScopesResponse();
        }

        protected override VariablesResponse HandleVariablesRequest(VariablesArguments arguments)
        {
            return new VariablesResponse();
        }

        protected override NextResponse HandleNextRequest(NextArguments arguments)
        {
            return new NextResponse();
        }

        protected override ContinueResponse HandleContinueRequest(ContinueArguments arguments)
        {
            return new ContinueResponse();
        }

        protected override StepInResponse HandleStepInRequest(StepInArguments arguments)
        {
            if (session.Engine.State == VMState.BREAK)
            {
                session.Debugger.Execute();

                switch (session.Engine.State)
                {
                    case VMState.BREAK:
                        Protocol.SendEvent(new StoppedEvent(StoppedEvent.ReasonValue.Step) { ThreadId = 1 });
                        break;
                    case VMState.HALT:
                        foreach (var item in session.Engine.ResultStack)
                        {
                            Protocol.SendEvent(new OutputEvent(item.GetResult()));
                        }
                        Protocol.SendEvent(new TerminatedEvent());
                        break;
                    default:
                        throw new NotImplementedException();
                }
            }

            return new StepInResponse();
        }

        protected override StepOutResponse HandleStepOutRequest(StepOutArguments arguments)
        {
            return new StepOutResponse();
        }

        protected override SetBreakpointsResponse HandleSetBreakpointsRequest(SetBreakpointsArguments arguments)
        {
            return new SetBreakpointsResponse();
        }
    }
}
