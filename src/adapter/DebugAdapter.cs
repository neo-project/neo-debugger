using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo.VM;
using NeoDebug.Models;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Linq;

namespace NeoDebug
{
    class DebugAdapter : DebugAdapterBase
    {
        private readonly Action<LogCategory, string> logger;
        private readonly DebugSession.DebugView defaultDebugView;
        private DebugSession? session;

        class DebugViewRequest : DebugRequest<DebugViewArguments>
        {
            public DebugViewRequest() : base("debugview")
            {
            }
        }

        class DebugViewArguments : DebugRequestArguments
        {
            [Newtonsoft.Json.JsonProperty("debugView")]
            public string DebugView { get; set; } = string.Empty;
        }

        public DebugAdapter(Stream @in, Stream @out, Action<LogCategory, string>? logger, DebugSession.DebugView defaultDebugView)
        {
            this.logger = logger ?? ((_, __) => { });
            this.defaultDebugView = defaultDebugView;

            InitializeProtocolClient(@in, @out);
            Protocol.LogMessage += (sender, args) => this.logger(args.Category, args.Message);

            Protocol.RegisterRequestType<DebugViewRequest, DebugViewArguments>(a =>
            {
                HandleDebugViewRequest(a.Arguments);
            });
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
            Protocol.SendEvent(new InitializedEvent());

            return new InitializeResponse()
            {
                SupportsEvaluateForHovers = true,
            };
        }

        protected override LaunchResponse HandleLaunchRequest(LaunchArguments arguments)
        {
            // since CreateDebugSession is an async method, we should be using HandleLaunchRequestAsync.
            // However, using HandleLaunchRequestAsync causes VSCode to send setBreakpoints and
            // threads request before receiving the launch response. 

            try
            {
                if (session != null) throw new InvalidOperationException();

                session = LaunchConfigurationParser.CreateDebugSession(arguments, Protocol.SendEvent, defaultDebugView)
                    .GetAwaiter().GetResult();
                session.Start();

                return new LaunchResponse();
            }
            catch (Exception ex)
            {
                Log(ex.Message, LogCategory.DebugAdapterOutput);
                throw new ProtocolException(ex.Message, ex);
            }
        }

        void HandleDebugViewRequest(DebugViewArguments arguments)
        {
            try
            {
                if (session == null) throw new InvalidOperationException();
                var debugView = Enum.Parse<DebugSession.DebugView>(arguments.DebugView, true);

                session.SetDebugView(debugView);
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

        protected override SourceResponse HandleSourceRequest(SourceArguments arguments)
        {
            try
            {
                if (session == null) throw new InvalidOperationException();
                return session.GetSource(arguments);
            }
            catch (Exception ex)
            {
                Log(ex.Message, LogCategory.DebugAdapterOutput);
                throw new ProtocolException(ex.Message, ex);
            }
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

        protected override ContinueResponse HandleContinueRequest(ContinueArguments arguments)
        {
            try
            {
                if (session == null) throw new InvalidOperationException();

                session.Continue();

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
