using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;

namespace NeoDebug.Neo3
{
    public class DebugAdapter : DebugAdapterBase
    {
        public delegate Task<IDebugSession> DebugSessionFactory(LaunchArguments launchArguments,
                                                                Action<DebugEvent> sendEvent,
                                                                DebugView defaultDebugView);

        private class DebugViewRequest : DebugRequest<DebugViewArguments>
        {
            public DebugViewRequest() : base("debugview")
            {
            }
        }

        private class DebugViewArguments : DebugRequestArguments
        {
            [Newtonsoft.Json.JsonProperty("debugView")]
            public string DebugView { get; set; } = string.Empty;
        }

        private readonly Action<LogCategory, string> logger;
        private readonly DebugView defaultDebugView;
        private IDebugSession? session;

        public DebugAdapter(System.IO.Stream @in,
                            System.IO.Stream @out,
                            Action<LogCategory, string>? logger,
                            DebugView defaultDebugView)
        {
            this.logger = logger ?? ((_, __) => { });
            this.defaultDebugView = defaultDebugView;

            InitializeProtocolClient(@in, @out);
            Protocol.LogMessage += (sender, args) => this.logger(args.Category, args.Message);

            Protocol.RegisterRequestType<DebugViewRequest, DebugViewArguments>(a => HandleDebugViewRequest(a.Arguments));
        }

        public void Run()
        {
            Protocol.Run();
            Protocol.WaitForReader();
        }

        private void Log(string message, LogCategory category = LogCategory.DebugAdapterOutput)
        {
            logger(category, message);
        }

        protected override InitializeResponse HandleInitializeRequest(InitializeArguments arguments)
        {
            return new InitializeResponse()
            {
                SupportsEvaluateForHovers = true,
                SupportsExceptionInfoRequest = true,
                ExceptionBreakpointFilters = new List<ExceptionBreakpointsFilter>
                {
                    new ExceptionBreakpointsFilter(
                        DebugSession.CAUGHT_EXCEPTION_FILTER,
                        "Caught Exceptions")
                    {
                        Default = false
                    },
                    new ExceptionBreakpointsFilter(
                        DebugSession.UNCAUGHT_EXCEPTION_FILTER,
                        "Uncaught Exceptions")
                    {
                        Default = true
                    }
                }
            };
        }

        protected override void HandleLaunchRequestAsync(IRequestResponder<LaunchArguments> responder)
        {
            if (session != null)
            {
                var ex = new InvalidOperationException();
                Log(ex.Message, LogCategory.DebugAdapterOutput);
                responder.SetError(new ProtocolException(ex.Message, ex));
                return;
            }

            _ = HandleLaunchRequestAsync(responder.Arguments)
                .ContinueWith(t =>
                {
                    if (t.IsCompletedSuccessfully)
                    {
                        responder.SetResponse(new LaunchResponse());
                    }
                    else
                    {
                        if (t.Exception is not null)
                        {
                            var exception = t.Exception is AggregateException aggregate
                                && aggregate.InnerExceptions.Count == 1
                                    ? aggregate.InnerExceptions[0]
                                    : t.Exception;
                            responder.SetError(new ProtocolException(exception.Message, exception));
                        }
                        else
                        {
                            responder.SetError(new ProtocolException($"Unknown error in {nameof(LaunchConfigParser.CreateDebugSessionAsync)}"));
                        }
                    }
                }, TaskScheduler.Current);
        }

        async Task HandleLaunchRequestAsync(LaunchArguments arguments)
        {
            session = await LaunchConfigParser.CreateDebugSessionAsync(arguments, Protocol.SendEvent, defaultDebugView);
            session.Start();
            Protocol.SendEvent(new InitializedEvent());
        }

        private void HandleDebugViewRequest(DebugViewArguments arguments)
        {
            try
            {
                if (session == null) throw new InvalidOperationException();
                var debugView = Enum.Parse<DebugView>(arguments.DebugView, true);

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

        protected override ExceptionInfoResponse HandleExceptionInfoRequest(ExceptionInfoArguments arguments)
        {
            try
            {
                if (session == null) throw new InvalidOperationException();
                var exceptionInfo = session.GetExceptionInfo();

                return new ExceptionInfoResponse()
                {
                    Description = exceptionInfo
                };
            }
            catch (Exception ex)
            {
                Log(ex.Message, LogCategory.DebugAdapterOutput);
                throw new ProtocolException(ex.Message, ex);
            }
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

        protected override ReverseContinueResponse HandleReverseContinueRequest(ReverseContinueArguments arguments)
        {
            try
            {
                if (session == null) throw new InvalidOperationException();

                session.ReverseContinue();
                return new ReverseContinueResponse();
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

        protected override StepBackResponse HandleStepBackRequest(StepBackArguments arguments)
        {
            try
            {
                if (session == null) throw new InvalidOperationException();

                session.StepBack();
                return new StepBackResponse();
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

        protected override SetExceptionBreakpointsResponse HandleSetExceptionBreakpointsRequest(SetExceptionBreakpointsArguments arguments)
        {
            try
            {
                if (session == null) throw new InvalidOperationException();

                session.SetExceptionBreakpoints(arguments.Filters);
                return new SetExceptionBreakpointsResponse();
            }
            catch (Exception ex)
            {
                Log(ex.Message, LogCategory.DebugAdapterOutput);
                throw new ProtocolException(ex.Message, ex);
            }
        }
    }
}
