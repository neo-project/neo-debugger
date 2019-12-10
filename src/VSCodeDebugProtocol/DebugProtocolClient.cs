using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Protocol;
using System;
using System.IO;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol
{
    public class DebugProtocolClient : DebugProtocol
    {
        public DebugProtocolClient(Stream stdIn, Stream stdOut)
            : this(stdIn, stdOut, registerStandardHandlers: true, DebugProtocolOptions.None)
        {
        }

        public DebugProtocolClient(Stream stdIn, Stream stdOut, bool registerStandardHandlers)
            : this(stdIn, stdOut, registerStandardHandlers, DebugProtocolOptions.None)
        {
        }

        public DebugProtocolClient(Stream stdIn, Stream stdOut, bool registerStandardHandlers, DebugProtocolOptions options)
            : base(stdIn, stdOut, options)
        {
            if (registerStandardHandlers)
            {
                RegisterStandardHandlers();
            }
        }

        private void RegisterStandardHandlers()
        {
            RegisterRequestType<AttachRequest, AttachArguments>(delegate (IRequestResponder<AttachArguments> r)
            {
                OnRequestReceived("attach", r);
            });
            RegisterRequestType<CompletionsRequest, CompletionsArguments, CompletionsResponse>(delegate (IRequestResponder<CompletionsArguments, CompletionsResponse> r)
            {
                OnRequestReceived("completions", r);
            });
            RegisterRequestType<ConfigurationDoneRequest, ConfigurationDoneArguments>(delegate (IRequestResponder<ConfigurationDoneArguments> r)
            {
                OnRequestReceived("configurationDone", r);
            });
            RegisterRequestType<ContinueRequest, ContinueArguments, ContinueResponse>(delegate (IRequestResponder<ContinueArguments, ContinueResponse> r)
            {
                OnRequestReceived("continue", r);
            });
            RegisterRequestType<DataBreakpointInfoRequest, DataBreakpointInfoArguments, DataBreakpointInfoResponse>(delegate (IRequestResponder<DataBreakpointInfoArguments, DataBreakpointInfoResponse> r)
            {
                OnRequestReceived("dataBreakpointInfo", r);
            });
            RegisterRequestType<DisassembleRequest, DisassembleArguments, DisassembleResponse>(delegate (IRequestResponder<DisassembleArguments, DisassembleResponse> r)
            {
                OnRequestReceived("disassemble", r);
            });
            RegisterRequestType<DisconnectRequest, DisconnectArguments>(delegate (IRequestResponder<DisconnectArguments> r)
            {
                OnRequestReceived("disconnect", r);
            });
            RegisterRequestType<EvaluateRequest, EvaluateArguments, EvaluateResponse>(delegate (IRequestResponder<EvaluateArguments, EvaluateResponse> r)
            {
                OnRequestReceived("evaluate", r);
            });
            RegisterRequestType<ExceptionInfoRequest, ExceptionInfoArguments, ExceptionInfoResponse>(delegate (IRequestResponder<ExceptionInfoArguments, ExceptionInfoResponse> r)
            {
                OnRequestReceived("exceptionInfo", r);
            });
            RegisterRequestType<GotoRequest, GotoArguments>(delegate (IRequestResponder<GotoArguments> r)
            {
                OnRequestReceived("goto", r);
            });
            RegisterRequestType<GotoTargetsRequest, GotoTargetsArguments, GotoTargetsResponse>(delegate (IRequestResponder<GotoTargetsArguments, GotoTargetsResponse> r)
            {
                OnRequestReceived("gotoTargets", r);
            });
            RegisterRequestType<InitializeRequest, InitializeArguments, InitializeResponse>(delegate (IRequestResponder<InitializeArguments, InitializeResponse> r)
            {
                OnRequestReceived("initialize", r);
            });
            RegisterRequestType<LaunchRequest, LaunchArguments>(delegate (IRequestResponder<LaunchArguments> r)
            {
                OnRequestReceived("launch", r);
            });
            RegisterRequestType<LoadedSourcesRequest, LoadedSourcesArguments, LoadedSourcesResponse>(delegate (IRequestResponder<LoadedSourcesArguments, LoadedSourcesResponse> r)
            {
                OnRequestReceived("loadedSources", r);
            });
            RegisterRequestType<LoadSymbolsRequest, LoadSymbolsArguments>(delegate (IRequestResponder<LoadSymbolsArguments> r)
            {
                OnRequestReceived("loadSymbols", r);
            });
            RegisterRequestType<ModulesRequest, ModulesArguments, ModulesResponse>(delegate (IRequestResponder<ModulesArguments, ModulesResponse> r)
            {
                OnRequestReceived("modules", r);
            });
            RegisterRequestType<ModuleSymbolSearchLogRequest, ModuleSymbolSearchLogArguments, ModuleSymbolSearchLogResponse>(delegate (IRequestResponder<ModuleSymbolSearchLogArguments, ModuleSymbolSearchLogResponse> r)
            {
                OnRequestReceived("moduleSymbolSearchLog", r);
            });
            RegisterRequestType<NextRequest, NextArguments>(delegate (IRequestResponder<NextArguments> r)
            {
                OnRequestReceived("next", r);
            });
            RegisterRequestType<PauseRequest, PauseArguments>(delegate (IRequestResponder<PauseArguments> r)
            {
                OnRequestReceived("pause", r);
            });
            RegisterRequestType<ReadMemoryRequest, ReadMemoryArguments, ReadMemoryResponse>(delegate (IRequestResponder<ReadMemoryArguments, ReadMemoryResponse> r)
            {
                OnRequestReceived("readMemory", r);
            });
            RegisterRequestType<RestartFrameRequest, RestartFrameArguments>(delegate (IRequestResponder<RestartFrameArguments> r)
            {
                OnRequestReceived("restartFrame", r);
            });
            RegisterRequestType<RestartRequest, RestartArguments>(delegate (IRequestResponder<RestartArguments> r)
            {
                OnRequestReceived("restart", r);
            });
            RegisterRequestType<ReverseContinueRequest, ReverseContinueArguments>(delegate (IRequestResponder<ReverseContinueArguments> r)
            {
                OnRequestReceived("reverseContinue", r);
            });
            RegisterRequestType<ScopesRequest, ScopesArguments, ScopesResponse>(delegate (IRequestResponder<ScopesArguments, ScopesResponse> r)
            {
                OnRequestReceived("scopes", r);
            });
            RegisterRequestType<SetBreakpointsRequest, SetBreakpointsArguments, SetBreakpointsResponse>(delegate (IRequestResponder<SetBreakpointsArguments, SetBreakpointsResponse> r)
            {
                OnRequestReceived("setBreakpoints", r);
            });
            RegisterRequestType<SetDataBreakpointsRequest, SetDataBreakpointsArguments, SetDataBreakpointsResponse>(delegate (IRequestResponder<SetDataBreakpointsArguments, SetDataBreakpointsResponse> r)
            {
                OnRequestReceived("setDataBreakpoints", r);
            });
            RegisterRequestType<SetDebuggerPropertyRequest, SetDebuggerPropertyArguments>(delegate (IRequestResponder<SetDebuggerPropertyArguments> r)
            {
                OnRequestReceived("setDebuggerProperty", r);
            });
            RegisterRequestType<SetExceptionBreakpointsRequest, SetExceptionBreakpointsArguments>(delegate (IRequestResponder<SetExceptionBreakpointsArguments> r)
            {
                OnRequestReceived("setExceptionBreakpoints", r);
            });
            RegisterRequestType<SetExpressionRequest, SetExpressionArguments, SetExpressionResponse>(delegate (IRequestResponder<SetExpressionArguments, SetExpressionResponse> r)
            {
                OnRequestReceived("setExpression", r);
            });
            RegisterRequestType<SetFunctionBreakpointsRequest, SetFunctionBreakpointsArguments, SetFunctionBreakpointsResponse>(delegate (IRequestResponder<SetFunctionBreakpointsArguments, SetFunctionBreakpointsResponse> r)
            {
                OnRequestReceived("setFunctionBreakpoints", r);
            });
            RegisterRequestType<SetSymbolOptionsRequest, SetSymbolOptionsArguments>(delegate (IRequestResponder<SetSymbolOptionsArguments> r)
            {
                OnRequestReceived("setSymbolOptions", r);
            });
            RegisterRequestType<SetVariableRequest, SetVariableArguments, SetVariableResponse>(delegate (IRequestResponder<SetVariableArguments, SetVariableResponse> r)
            {
                OnRequestReceived("setVariable", r);
            });
            RegisterRequestType<SourceRequest, SourceArguments, SourceResponse>(delegate (IRequestResponder<SourceArguments, SourceResponse> r)
            {
                OnRequestReceived("source", r);
            });
            RegisterRequestType<StackTraceRequest, StackTraceArguments, StackTraceResponse>(delegate (IRequestResponder<StackTraceArguments, StackTraceResponse> r)
            {
                OnRequestReceived("stackTrace", r);
            });
            RegisterRequestType<StepBackRequest, StepBackArguments>(delegate (IRequestResponder<StepBackArguments> r)
            {
                OnRequestReceived("stepBack", r);
            });
            RegisterRequestType<StepInRequest, StepInArguments>(delegate (IRequestResponder<StepInArguments> r)
            {
                OnRequestReceived("stepIn", r);
            });
            RegisterRequestType<StepInTargetsRequest, StepInTargetsArguments, StepInTargetsResponse>(delegate (IRequestResponder<StepInTargetsArguments, StepInTargetsResponse> r)
            {
                OnRequestReceived("stepInTargets", r);
            });
            RegisterRequestType<StepOutRequest, StepOutArguments>(delegate (IRequestResponder<StepOutArguments> r)
            {
                OnRequestReceived("stepOut", r);
            });
            RegisterRequestType<TerminateRequest, TerminateArguments>(delegate (IRequestResponder<TerminateArguments> r)
            {
                OnRequestReceived("terminate", r);
            });
            RegisterRequestType<TerminateThreadsRequest, TerminateThreadsArguments>(delegate (IRequestResponder<TerminateThreadsArguments> r)
            {
                OnRequestReceived("terminateThreads", r);
            });
            RegisterRequestType<ThreadsRequest, ThreadsArguments, ThreadsResponse>(delegate (IRequestResponder<ThreadsArguments, ThreadsResponse> r)
            {
                OnRequestReceived("threads", r);
            });
            RegisterRequestType<VariablesRequest, VariablesArguments, VariablesResponse>(delegate (IRequestResponder<VariablesArguments, VariablesResponse> r)
            {
                OnRequestReceived("variables", r);
            });
        }

        public void SendEvent(DebugEvent evt)
        {
            SendEventCore(evt);
        }

        public void SendClientRequest<TArgs, TResponse>(DebugClientRequestWithResponse<TArgs, TResponse> request, Action<TArgs, TResponse> completionFunc, Action<TArgs, ProtocolException> errorFunc = null) where TArgs : class, new() where TResponse : ResponseBody
        {
            SendRequestCore(request, completionFunc, errorFunc);
        }

        public TResponse SendClientRequestSync<TArgs, TResponse>(DebugClientRequestWithResponse<TArgs, TResponse> request) where TArgs : class, new() where TResponse : ResponseBody
        {
            return SendRequestSyncCore(request);
        }

        public void SendClientRequest<TArgs>(DebugClientRequest<TArgs> request, Action<TArgs> completionFunc, Action<TArgs, ProtocolException> errorFunc = null) where TArgs : class, new()
        {
            SendRequestCore(request, completionFunc, errorFunc);
        }

        public void SendClientRequestSync<TArgs>(DebugClientRequest<TArgs> request) where TArgs : class, new()
        {
            SendRequestSyncCore(request);
        }

        public void RegisterRequestType<TRequest, TArgs>(Action<IRequestResponder<TArgs>> handler) where TRequest : DebugRequest<TArgs>, new() where TArgs : class, new()
        {
            RegisterRequestTypeCore(new TRequest().Command, new RequestInfo<TArgs>(handler));
        }

        public void RegisterRequestType<TRequest, TArgs, TResponse>(Action<IRequestResponder<TArgs, TResponse>> handler) where TRequest : DebugRequestWithResponse<TArgs, TResponse>, new() where TArgs : class, new() where TResponse : ResponseBody
        {
            RegisterRequestTypeCore(new TRequest().Command, new RequestWithResponseInfo<TArgs, TResponse>(handler));
        }
    }
}
