using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Protocol;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Utilities;
using System;
using System.IO;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol
{
    public abstract class DebugAdapterBase
    {
        public DebugProtocolClient Protocol
        {
            get;
            private set;
        }

        protected void InitializeProtocolClient(Stream debugAdapterStdIn, Stream debugAdapterStdOut)
        {
            InitializeProtocolClient(debugAdapterStdIn, debugAdapterStdOut, DebugProtocolOptions.None);
        }

        protected void InitializeProtocolClient(Stream debugAdapterStdIn, Stream debugAdapterStdOut, DebugProtocolOptions options)
        {
            if (Protocol != null)
            {
                throw new InvalidOperationException("ProtocolClient is already initialized!");
            }
            Protocol = new DebugProtocolClient(debugAdapterStdIn, debugAdapterStdOut, registerStandardHandlers: false, options);
            Protocol.DispatcherError += OnDispatcherError;
            Protocol.RequestReceived += OnProtocolRequestReceived;
            Protocol.RegisterRequestType<AttachRequest, AttachArguments>(delegate (IRequestResponder<AttachArguments> r)
            {
                HandleAttachRequestAsync(r);
            });
            Protocol.RegisterRequestType<CompletionsRequest, CompletionsArguments, CompletionsResponse>(delegate (IRequestResponder<CompletionsArguments, CompletionsResponse> r)
            {
                HandleCompletionsRequestAsync(r);
            });
            Protocol.RegisterRequestType<ConfigurationDoneRequest, ConfigurationDoneArguments>(delegate (IRequestResponder<ConfigurationDoneArguments> r)
            {
                HandleConfigurationDoneRequestAsync(r);
            });
            Protocol.RegisterRequestType<ContinueRequest, ContinueArguments, ContinueResponse>(delegate (IRequestResponder<ContinueArguments, ContinueResponse> r)
            {
                HandleContinueRequestAsync(r);
            });
            Protocol.RegisterRequestType<DataBreakpointInfoRequest, DataBreakpointInfoArguments, DataBreakpointInfoResponse>(delegate (IRequestResponder<DataBreakpointInfoArguments, DataBreakpointInfoResponse> r)
            {
                HandleDataBreakpointInfoRequestAsync(r);
            });
            Protocol.RegisterRequestType<DisassembleRequest, DisassembleArguments, DisassembleResponse>(delegate (IRequestResponder<DisassembleArguments, DisassembleResponse> r)
            {
                HandleDisassembleRequestAsync(r);
            });
            Protocol.RegisterRequestType<DisconnectRequest, DisconnectArguments>(delegate (IRequestResponder<DisconnectArguments> r)
            {
                HandleDisconnectRequestAsync(r);
            });
            Protocol.RegisterRequestType<EvaluateRequest, EvaluateArguments, EvaluateResponse>(delegate (IRequestResponder<EvaluateArguments, EvaluateResponse> r)
            {
                HandleEvaluateRequestAsync(r);
            });
            Protocol.RegisterRequestType<ExceptionInfoRequest, ExceptionInfoArguments, ExceptionInfoResponse>(delegate (IRequestResponder<ExceptionInfoArguments, ExceptionInfoResponse> r)
            {
                HandleExceptionInfoRequestAsync(r);
            });
            Protocol.RegisterRequestType<GotoRequest, GotoArguments>(delegate (IRequestResponder<GotoArguments> r)
            {
                HandleGotoRequestAsync(r);
            });
            Protocol.RegisterRequestType<GotoTargetsRequest, GotoTargetsArguments, GotoTargetsResponse>(delegate (IRequestResponder<GotoTargetsArguments, GotoTargetsResponse> r)
            {
                HandleGotoTargetsRequestAsync(r);
            });
            Protocol.RegisterRequestType<InitializeRequest, InitializeArguments, InitializeResponse>(delegate (IRequestResponder<InitializeArguments, InitializeResponse> r)
            {
                HandleInitializeRequestAsync(r);
            });
            Protocol.RegisterRequestType<LaunchRequest, LaunchArguments>(delegate (IRequestResponder<LaunchArguments> r)
            {
                HandleLaunchRequestAsync(r);
            });
            Protocol.RegisterRequestType<LoadedSourcesRequest, LoadedSourcesArguments, LoadedSourcesResponse>(delegate (IRequestResponder<LoadedSourcesArguments, LoadedSourcesResponse> r)
            {
                HandleLoadedSourcesRequestAsync(r);
            });
            Protocol.RegisterRequestType<LoadSymbolsRequest, LoadSymbolsArguments>(delegate (IRequestResponder<LoadSymbolsArguments> r)
            {
                HandleLoadSymbolsRequestAsync(r);
            });
            Protocol.RegisterRequestType<ModulesRequest, ModulesArguments, ModulesResponse>(delegate (IRequestResponder<ModulesArguments, ModulesResponse> r)
            {
                HandleModulesRequestAsync(r);
            });
            Protocol.RegisterRequestType<ModuleSymbolSearchLogRequest, ModuleSymbolSearchLogArguments, ModuleSymbolSearchLogResponse>(delegate (IRequestResponder<ModuleSymbolSearchLogArguments, ModuleSymbolSearchLogResponse> r)
            {
                HandleModuleSymbolSearchLogRequestAsync(r);
            });
            Protocol.RegisterRequestType<NextRequest, NextArguments>(delegate (IRequestResponder<NextArguments> r)
            {
                HandleNextRequestAsync(r);
            });
            Protocol.RegisterRequestType<PauseRequest, PauseArguments>(delegate (IRequestResponder<PauseArguments> r)
            {
                HandlePauseRequestAsync(r);
            });
            Protocol.RegisterRequestType<ReadMemoryRequest, ReadMemoryArguments, ReadMemoryResponse>(delegate (IRequestResponder<ReadMemoryArguments, ReadMemoryResponse> r)
            {
                HandleReadMemoryRequestAsync(r);
            });
            Protocol.RegisterRequestType<RestartFrameRequest, RestartFrameArguments>(delegate (IRequestResponder<RestartFrameArguments> r)
            {
                HandleRestartFrameRequestAsync(r);
            });
            Protocol.RegisterRequestType<RestartRequest, RestartArguments>(delegate (IRequestResponder<RestartArguments> r)
            {
                HandleRestartRequestAsync(r);
            });
            Protocol.RegisterRequestType<ReverseContinueRequest, ReverseContinueArguments>(delegate (IRequestResponder<ReverseContinueArguments> r)
            {
                HandleReverseContinueRequestAsync(r);
            });
            Protocol.RegisterRequestType<ScopesRequest, ScopesArguments, ScopesResponse>(delegate (IRequestResponder<ScopesArguments, ScopesResponse> r)
            {
                HandleScopesRequestAsync(r);
            });
            Protocol.RegisterRequestType<SetBreakpointsRequest, SetBreakpointsArguments, SetBreakpointsResponse>(delegate (IRequestResponder<SetBreakpointsArguments, SetBreakpointsResponse> r)
            {
                HandleSetBreakpointsRequestAsync(r);
            });
            Protocol.RegisterRequestType<SetDataBreakpointsRequest, SetDataBreakpointsArguments, SetDataBreakpointsResponse>(delegate (IRequestResponder<SetDataBreakpointsArguments, SetDataBreakpointsResponse> r)
            {
                HandleSetDataBreakpointsRequestAsync(r);
            });
            Protocol.RegisterRequestType<SetDebuggerPropertyRequest, SetDebuggerPropertyArguments>(delegate (IRequestResponder<SetDebuggerPropertyArguments> r)
            {
                HandleSetDebuggerPropertyRequestAsync(r);
            });
            Protocol.RegisterRequestType<SetExceptionBreakpointsRequest, SetExceptionBreakpointsArguments>(delegate (IRequestResponder<SetExceptionBreakpointsArguments> r)
            {
                HandleSetExceptionBreakpointsRequestAsync(r);
            });
            Protocol.RegisterRequestType<SetExpressionRequest, SetExpressionArguments, SetExpressionResponse>(delegate (IRequestResponder<SetExpressionArguments, SetExpressionResponse> r)
            {
                HandleSetExpressionRequestAsync(r);
            });
            Protocol.RegisterRequestType<SetFunctionBreakpointsRequest, SetFunctionBreakpointsArguments, SetFunctionBreakpointsResponse>(delegate (IRequestResponder<SetFunctionBreakpointsArguments, SetFunctionBreakpointsResponse> r)
            {
                HandleSetFunctionBreakpointsRequestAsync(r);
            });
            Protocol.RegisterRequestType<SetSymbolOptionsRequest, SetSymbolOptionsArguments>(delegate (IRequestResponder<SetSymbolOptionsArguments> r)
            {
                HandleSetSymbolOptionsRequestAsync(r);
            });
            Protocol.RegisterRequestType<SetVariableRequest, SetVariableArguments, SetVariableResponse>(delegate (IRequestResponder<SetVariableArguments, SetVariableResponse> r)
            {
                HandleSetVariableRequestAsync(r);
            });
            Protocol.RegisterRequestType<SourceRequest, SourceArguments, SourceResponse>(delegate (IRequestResponder<SourceArguments, SourceResponse> r)
            {
                HandleSourceRequestAsync(r);
            });
            Protocol.RegisterRequestType<StackTraceRequest, StackTraceArguments, StackTraceResponse>(delegate (IRequestResponder<StackTraceArguments, StackTraceResponse> r)
            {
                HandleStackTraceRequestAsync(r);
            });
            Protocol.RegisterRequestType<StepBackRequest, StepBackArguments>(delegate (IRequestResponder<StepBackArguments> r)
            {
                HandleStepBackRequestAsync(r);
            });
            Protocol.RegisterRequestType<StepInRequest, StepInArguments>(delegate (IRequestResponder<StepInArguments> r)
            {
                HandleStepInRequestAsync(r);
            });
            Protocol.RegisterRequestType<StepInTargetsRequest, StepInTargetsArguments, StepInTargetsResponse>(delegate (IRequestResponder<StepInTargetsArguments, StepInTargetsResponse> r)
            {
                HandleStepInTargetsRequestAsync(r);
            });
            Protocol.RegisterRequestType<StepOutRequest, StepOutArguments>(delegate (IRequestResponder<StepOutArguments> r)
            {
                HandleStepOutRequestAsync(r);
            });
            Protocol.RegisterRequestType<TerminateRequest, TerminateArguments>(delegate (IRequestResponder<TerminateArguments> r)
            {
                HandleTerminateRequestAsync(r);
            });
            Protocol.RegisterRequestType<TerminateThreadsRequest, TerminateThreadsArguments>(delegate (IRequestResponder<TerminateThreadsArguments> r)
            {
                HandleTerminateThreadsRequestAsync(r);
            });
            Protocol.RegisterRequestType<ThreadsRequest, ThreadsArguments, ThreadsResponse>(delegate (IRequestResponder<ThreadsArguments, ThreadsResponse> r)
            {
                HandleThreadsRequestAsync(r);
            });
            Protocol.RegisterRequestType<VariablesRequest, VariablesArguments, VariablesResponse>(delegate (IRequestResponder<VariablesArguments, VariablesResponse> r)
            {
                HandleVariablesRequestAsync(r);
            });
        }

        private void OnDispatcherError(object sender, DispatcherErrorEventArgs e)
        {
            HandleProtocolError(e.Exception);
        }

        protected virtual void HandleProtocolError(Exception ex)
        {
        }

        private void OnProtocolRequestReceived(object sender, RequestReceivedEventArgs e)
        {
            e.Response = HandleProtocolRequest(e.Command, e.Args);
        }

        protected virtual ResponseBody HandleProtocolRequest(string requestType, object requestArgs)
        {
            switch (requestType)
            {
                case "attach":
                    return HandleAttachRequest((AttachArguments)requestArgs);
                case "completions":
                    return HandleCompletionsRequest((CompletionsArguments)requestArgs);
                case "configurationDone":
                    return HandleConfigurationDoneRequest((ConfigurationDoneArguments)requestArgs);
                case "continue":
                    return HandleContinueRequest((ContinueArguments)requestArgs);
                case "dataBreakpointInfo":
                    return HandleDataBreakpointInfoRequest((DataBreakpointInfoArguments)requestArgs);
                case "disassemble":
                    return HandleDisassembleRequest((DisassembleArguments)requestArgs);
                case "disconnect":
                    return HandleDisconnectRequest((DisconnectArguments)requestArgs);
                case "evaluate":
                    return HandleEvaluateRequest((EvaluateArguments)requestArgs);
                case "exceptionInfo":
                    return HandleExceptionInfoRequest((ExceptionInfoArguments)requestArgs);
                case "goto":
                    return HandleGotoRequest((GotoArguments)requestArgs);
                case "gotoTargets":
                    return HandleGotoTargetsRequest((GotoTargetsArguments)requestArgs);
                case "initialize":
                    return HandleInitializeRequest((InitializeArguments)requestArgs);
                case "launch":
                    return HandleLaunchRequest((LaunchArguments)requestArgs);
                case "loadedSources":
                    return HandleLoadedSourcesRequest((LoadedSourcesArguments)requestArgs);
                case "loadSymbols":
                    return HandleLoadSymbolsRequest((LoadSymbolsArguments)requestArgs);
                case "modules":
                    return HandleModulesRequest((ModulesArguments)requestArgs);
                case "moduleSymbolSearchLog":
                    return HandleModuleSymbolSearchLogRequest((ModuleSymbolSearchLogArguments)requestArgs);
                case "next":
                    return HandleNextRequest((NextArguments)requestArgs);
                case "pause":
                    return HandlePauseRequest((PauseArguments)requestArgs);
                case "readMemory":
                    return HandleReadMemoryRequest((ReadMemoryArguments)requestArgs);
                case "restartFrame":
                    return HandleRestartFrameRequest((RestartFrameArguments)requestArgs);
                case "restart":
                    return HandleRestartRequest((RestartArguments)requestArgs);
                case "reverseContinue":
                    return HandleReverseContinueRequest((ReverseContinueArguments)requestArgs);
                case "scopes":
                    return HandleScopesRequest((ScopesArguments)requestArgs);
                case "setBreakpoints":
                    return HandleSetBreakpointsRequest((SetBreakpointsArguments)requestArgs);
                case "setDataBreakpoints":
                    return HandleSetDataBreakpointsRequest((SetDataBreakpointsArguments)requestArgs);
                case "setDebuggerProperty":
                    return HandleSetDebuggerPropertyRequest((SetDebuggerPropertyArguments)requestArgs);
                case "setExceptionBreakpoints":
                    return HandleSetExceptionBreakpointsRequest((SetExceptionBreakpointsArguments)requestArgs);
                case "setExpression":
                    return HandleSetExpressionRequest((SetExpressionArguments)requestArgs);
                case "setFunctionBreakpoints":
                    return HandleSetFunctionBreakpointsRequest((SetFunctionBreakpointsArguments)requestArgs);
                case "setSymbolOptions":
                    return HandleSetSymbolOptionsRequest((SetSymbolOptionsArguments)requestArgs);
                case "setVariable":
                    return HandleSetVariableRequest((SetVariableArguments)requestArgs);
                case "source":
                    return HandleSourceRequest((SourceArguments)requestArgs);
                case "stackTrace":
                    return HandleStackTraceRequest((StackTraceArguments)requestArgs);
                case "stepBack":
                    return HandleStepBackRequest((StepBackArguments)requestArgs);
                case "stepIn":
                    return HandleStepInRequest((StepInArguments)requestArgs);
                case "stepInTargets":
                    return HandleStepInTargetsRequest((StepInTargetsArguments)requestArgs);
                case "stepOut":
                    return HandleStepOutRequest((StepOutArguments)requestArgs);
                case "terminate":
                    return HandleTerminateRequest((TerminateArguments)requestArgs);
                case "terminateThreads":
                    return HandleTerminateThreadsRequest((TerminateThreadsArguments)requestArgs);
                case "threads":
                    return HandleThreadsRequest((ThreadsArguments)requestArgs);
                case "variables":
                    return HandleVariablesRequest((VariablesArguments)requestArgs);
                default:
                    throw new InvalidOperationException("Unknown request type '{0}'!".FormatInvariantWithArgs(requestType));
            }
        }

        protected virtual void HandleAttachRequestAsync(IRequestResponder<AttachArguments> responder)
        {
            Protocol.OnRequestReceived("attach", responder);
        }

        protected virtual AttachResponse HandleAttachRequest(AttachArguments arguments)
        {
            throw new NotImplementedException("No handler implemented for request type 'AttachRequest'!");
        }

        protected virtual void HandleCompletionsRequestAsync(IRequestResponder<CompletionsArguments, CompletionsResponse> responder)
        {
            Protocol.OnRequestReceived("completions", responder);
        }

        protected virtual CompletionsResponse HandleCompletionsRequest(CompletionsArguments arguments)
        {
            throw new NotImplementedException("No handler implemented for request type 'CompletionsRequest'!");
        }

        protected virtual void HandleConfigurationDoneRequestAsync(IRequestResponder<ConfigurationDoneArguments> responder)
        {
            Protocol.OnRequestReceived("configurationDone", responder);
        }

        protected virtual ConfigurationDoneResponse HandleConfigurationDoneRequest(ConfigurationDoneArguments arguments)
        {
            throw new NotImplementedException("No handler implemented for request type 'ConfigurationDoneRequest'!");
        }

        protected virtual void HandleContinueRequestAsync(IRequestResponder<ContinueArguments, ContinueResponse> responder)
        {
            Protocol.OnRequestReceived("continue", responder);
        }

        protected virtual ContinueResponse HandleContinueRequest(ContinueArguments arguments)
        {
            throw new NotImplementedException("No handler implemented for request type 'ContinueRequest'!");
        }

        protected virtual void HandleDataBreakpointInfoRequestAsync(IRequestResponder<DataBreakpointInfoArguments, DataBreakpointInfoResponse> responder)
        {
            Protocol.OnRequestReceived("dataBreakpointInfo", responder);
        }

        protected virtual DataBreakpointInfoResponse HandleDataBreakpointInfoRequest(DataBreakpointInfoArguments arguments)
        {
            throw new NotImplementedException("No handler implemented for request type 'DataBreakpointInfoRequest'!");
        }

        protected virtual void HandleDisassembleRequestAsync(IRequestResponder<DisassembleArguments, DisassembleResponse> responder)
        {
            Protocol.OnRequestReceived("disassemble", responder);
        }

        protected virtual DisassembleResponse HandleDisassembleRequest(DisassembleArguments arguments)
        {
            throw new NotImplementedException("No handler implemented for request type 'DisassembleRequest'!");
        }

        protected virtual void HandleDisconnectRequestAsync(IRequestResponder<DisconnectArguments> responder)
        {
            Protocol.OnRequestReceived("disconnect", responder);
        }

        protected virtual DisconnectResponse HandleDisconnectRequest(DisconnectArguments arguments)
        {
            throw new NotImplementedException("No handler implemented for request type 'DisconnectRequest'!");
        }

        protected virtual void HandleEvaluateRequestAsync(IRequestResponder<EvaluateArguments, EvaluateResponse> responder)
        {
            Protocol.OnRequestReceived("evaluate", responder);
        }

        protected virtual EvaluateResponse HandleEvaluateRequest(EvaluateArguments arguments)
        {
            throw new NotImplementedException("No handler implemented for request type 'EvaluateRequest'!");
        }

        protected virtual void HandleExceptionInfoRequestAsync(IRequestResponder<ExceptionInfoArguments, ExceptionInfoResponse> responder)
        {
            Protocol.OnRequestReceived("exceptionInfo", responder);
        }

        protected virtual ExceptionInfoResponse HandleExceptionInfoRequest(ExceptionInfoArguments arguments)
        {
            throw new NotImplementedException("No handler implemented for request type 'ExceptionInfoRequest'!");
        }

        protected virtual void HandleGotoRequestAsync(IRequestResponder<GotoArguments> responder)
        {
            Protocol.OnRequestReceived("goto", responder);
        }

        protected virtual GotoResponse HandleGotoRequest(GotoArguments arguments)
        {
            throw new NotImplementedException("No handler implemented for request type 'GotoRequest'!");
        }

        protected virtual void HandleGotoTargetsRequestAsync(IRequestResponder<GotoTargetsArguments, GotoTargetsResponse> responder)
        {
            Protocol.OnRequestReceived("gotoTargets", responder);
        }

        protected virtual GotoTargetsResponse HandleGotoTargetsRequest(GotoTargetsArguments arguments)
        {
            throw new NotImplementedException("No handler implemented for request type 'GotoTargetsRequest'!");
        }

        protected virtual void HandleInitializeRequestAsync(IRequestResponder<InitializeArguments, InitializeResponse> responder)
        {
            Protocol.OnRequestReceived("initialize", responder);
        }

        protected virtual InitializeResponse HandleInitializeRequest(InitializeArguments arguments)
        {
            throw new NotImplementedException("No handler implemented for request type 'InitializeRequest'!");
        }

        protected virtual void HandleLaunchRequestAsync(IRequestResponder<LaunchArguments> responder)
        {
            Protocol.OnRequestReceived("launch", responder);
        }

        protected virtual LaunchResponse HandleLaunchRequest(LaunchArguments arguments)
        {
            throw new NotImplementedException("No handler implemented for request type 'LaunchRequest'!");
        }

        protected virtual void HandleLoadedSourcesRequestAsync(IRequestResponder<LoadedSourcesArguments, LoadedSourcesResponse> responder)
        {
            Protocol.OnRequestReceived("loadedSources", responder);
        }

        protected virtual LoadedSourcesResponse HandleLoadedSourcesRequest(LoadedSourcesArguments arguments)
        {
            throw new NotImplementedException("No handler implemented for request type 'LoadedSourcesRequest'!");
        }

        protected virtual void HandleLoadSymbolsRequestAsync(IRequestResponder<LoadSymbolsArguments> responder)
        {
            Protocol.OnRequestReceived("loadSymbols", responder);
        }

        protected virtual LoadSymbolsResponse HandleLoadSymbolsRequest(LoadSymbolsArguments arguments)
        {
            throw new NotImplementedException("No handler implemented for request type 'LoadSymbolsRequest'!");
        }

        protected virtual void HandleModulesRequestAsync(IRequestResponder<ModulesArguments, ModulesResponse> responder)
        {
            Protocol.OnRequestReceived("modules", responder);
        }

        protected virtual ModulesResponse HandleModulesRequest(ModulesArguments arguments)
        {
            throw new NotImplementedException("No handler implemented for request type 'ModulesRequest'!");
        }

        protected virtual void HandleModuleSymbolSearchLogRequestAsync(IRequestResponder<ModuleSymbolSearchLogArguments, ModuleSymbolSearchLogResponse> responder)
        {
            Protocol.OnRequestReceived("moduleSymbolSearchLog", responder);
        }

        protected virtual ModuleSymbolSearchLogResponse HandleModuleSymbolSearchLogRequest(ModuleSymbolSearchLogArguments arguments)
        {
            throw new NotImplementedException("No handler implemented for request type 'ModuleSymbolSearchLogRequest'!");
        }

        protected virtual void HandleNextRequestAsync(IRequestResponder<NextArguments> responder)
        {
            Protocol.OnRequestReceived("next", responder);
        }

        protected virtual NextResponse HandleNextRequest(NextArguments arguments)
        {
            throw new NotImplementedException("No handler implemented for request type 'NextRequest'!");
        }

        protected virtual void HandlePauseRequestAsync(IRequestResponder<PauseArguments> responder)
        {
            Protocol.OnRequestReceived("pause", responder);
        }

        protected virtual PauseResponse HandlePauseRequest(PauseArguments arguments)
        {
            throw new NotImplementedException("No handler implemented for request type 'PauseRequest'!");
        }

        protected virtual void HandleReadMemoryRequestAsync(IRequestResponder<ReadMemoryArguments, ReadMemoryResponse> responder)
        {
            Protocol.OnRequestReceived("readMemory", responder);
        }

        protected virtual ReadMemoryResponse HandleReadMemoryRequest(ReadMemoryArguments arguments)
        {
            throw new NotImplementedException("No handler implemented for request type 'ReadMemoryRequest'!");
        }

        protected virtual void HandleRestartFrameRequestAsync(IRequestResponder<RestartFrameArguments> responder)
        {
            Protocol.OnRequestReceived("restartFrame", responder);
        }

        protected virtual RestartFrameResponse HandleRestartFrameRequest(RestartFrameArguments arguments)
        {
            throw new NotImplementedException("No handler implemented for request type 'RestartFrameRequest'!");
        }

        protected virtual void HandleRestartRequestAsync(IRequestResponder<RestartArguments> responder)
        {
            Protocol.OnRequestReceived("restart", responder);
        }

        protected virtual RestartResponse HandleRestartRequest(RestartArguments arguments)
        {
            throw new NotImplementedException("No handler implemented for request type 'RestartRequest'!");
        }

        protected virtual void HandleReverseContinueRequestAsync(IRequestResponder<ReverseContinueArguments> responder)
        {
            Protocol.OnRequestReceived("reverseContinue", responder);
        }

        protected virtual ReverseContinueResponse HandleReverseContinueRequest(ReverseContinueArguments arguments)
        {
            throw new NotImplementedException("No handler implemented for request type 'ReverseContinueRequest'!");
        }

        protected virtual void HandleScopesRequestAsync(IRequestResponder<ScopesArguments, ScopesResponse> responder)
        {
            Protocol.OnRequestReceived("scopes", responder);
        }

        protected virtual ScopesResponse HandleScopesRequest(ScopesArguments arguments)
        {
            throw new NotImplementedException("No handler implemented for request type 'ScopesRequest'!");
        }

        protected virtual void HandleSetBreakpointsRequestAsync(IRequestResponder<SetBreakpointsArguments, SetBreakpointsResponse> responder)
        {
            Protocol.OnRequestReceived("setBreakpoints", responder);
        }

        protected virtual SetBreakpointsResponse HandleSetBreakpointsRequest(SetBreakpointsArguments arguments)
        {
            throw new NotImplementedException("No handler implemented for request type 'SetBreakpointsRequest'!");
        }

        protected virtual void HandleSetDataBreakpointsRequestAsync(IRequestResponder<SetDataBreakpointsArguments, SetDataBreakpointsResponse> responder)
        {
            Protocol.OnRequestReceived("setDataBreakpoints", responder);
        }

        protected virtual SetDataBreakpointsResponse HandleSetDataBreakpointsRequest(SetDataBreakpointsArguments arguments)
        {
            throw new NotImplementedException("No handler implemented for request type 'SetDataBreakpointsRequest'!");
        }

        protected virtual void HandleSetDebuggerPropertyRequestAsync(IRequestResponder<SetDebuggerPropertyArguments> responder)
        {
            Protocol.OnRequestReceived("setDebuggerProperty", responder);
        }

        protected virtual SetDebuggerPropertyResponse HandleSetDebuggerPropertyRequest(SetDebuggerPropertyArguments arguments)
        {
            throw new NotImplementedException("No handler implemented for request type 'SetDebuggerPropertyRequest'!");
        }

        protected virtual void HandleSetExceptionBreakpointsRequestAsync(IRequestResponder<SetExceptionBreakpointsArguments> responder)
        {
            Protocol.OnRequestReceived("setExceptionBreakpoints", responder);
        }

        protected virtual SetExceptionBreakpointsResponse HandleSetExceptionBreakpointsRequest(SetExceptionBreakpointsArguments arguments)
        {
            throw new NotImplementedException("No handler implemented for request type 'SetExceptionBreakpointsRequest'!");
        }

        protected virtual void HandleSetExpressionRequestAsync(IRequestResponder<SetExpressionArguments, SetExpressionResponse> responder)
        {
            Protocol.OnRequestReceived("setExpression", responder);
        }

        protected virtual SetExpressionResponse HandleSetExpressionRequest(SetExpressionArguments arguments)
        {
            throw new NotImplementedException("No handler implemented for request type 'SetExpressionRequest'!");
        }

        protected virtual void HandleSetFunctionBreakpointsRequestAsync(IRequestResponder<SetFunctionBreakpointsArguments, SetFunctionBreakpointsResponse> responder)
        {
            Protocol.OnRequestReceived("setFunctionBreakpoints", responder);
        }

        protected virtual SetFunctionBreakpointsResponse HandleSetFunctionBreakpointsRequest(SetFunctionBreakpointsArguments arguments)
        {
            throw new NotImplementedException("No handler implemented for request type 'SetFunctionBreakpointsRequest'!");
        }

        protected virtual void HandleSetSymbolOptionsRequestAsync(IRequestResponder<SetSymbolOptionsArguments> responder)
        {
            Protocol.OnRequestReceived("setSymbolOptions", responder);
        }

        protected virtual SetSymbolOptionsResponse HandleSetSymbolOptionsRequest(SetSymbolOptionsArguments arguments)
        {
            throw new NotImplementedException("No handler implemented for request type 'SetSymbolOptionsRequest'!");
        }

        protected virtual void HandleSetVariableRequestAsync(IRequestResponder<SetVariableArguments, SetVariableResponse> responder)
        {
            Protocol.OnRequestReceived("setVariable", responder);
        }

        protected virtual SetVariableResponse HandleSetVariableRequest(SetVariableArguments arguments)
        {
            throw new NotImplementedException("No handler implemented for request type 'SetVariableRequest'!");
        }

        protected virtual void HandleSourceRequestAsync(IRequestResponder<SourceArguments, SourceResponse> responder)
        {
            Protocol.OnRequestReceived("source", responder);
        }

        protected virtual SourceResponse HandleSourceRequest(SourceArguments arguments)
        {
            throw new NotImplementedException("No handler implemented for request type 'SourceRequest'!");
        }

        protected virtual void HandleStackTraceRequestAsync(IRequestResponder<StackTraceArguments, StackTraceResponse> responder)
        {
            Protocol.OnRequestReceived("stackTrace", responder);
        }

        protected virtual StackTraceResponse HandleStackTraceRequest(StackTraceArguments arguments)
        {
            throw new NotImplementedException("No handler implemented for request type 'StackTraceRequest'!");
        }

        protected virtual void HandleStepBackRequestAsync(IRequestResponder<StepBackArguments> responder)
        {
            Protocol.OnRequestReceived("stepBack", responder);
        }

        protected virtual StepBackResponse HandleStepBackRequest(StepBackArguments arguments)
        {
            throw new NotImplementedException("No handler implemented for request type 'StepBackRequest'!");
        }

        protected virtual void HandleStepInRequestAsync(IRequestResponder<StepInArguments> responder)
        {
            Protocol.OnRequestReceived("stepIn", responder);
        }

        protected virtual StepInResponse HandleStepInRequest(StepInArguments arguments)
        {
            throw new NotImplementedException("No handler implemented for request type 'StepInRequest'!");
        }

        protected virtual void HandleStepInTargetsRequestAsync(IRequestResponder<StepInTargetsArguments, StepInTargetsResponse> responder)
        {
            Protocol.OnRequestReceived("stepInTargets", responder);
        }

        protected virtual StepInTargetsResponse HandleStepInTargetsRequest(StepInTargetsArguments arguments)
        {
            throw new NotImplementedException("No handler implemented for request type 'StepInTargetsRequest'!");
        }

        protected virtual void HandleStepOutRequestAsync(IRequestResponder<StepOutArguments> responder)
        {
            Protocol.OnRequestReceived("stepOut", responder);
        }

        protected virtual StepOutResponse HandleStepOutRequest(StepOutArguments arguments)
        {
            throw new NotImplementedException("No handler implemented for request type 'StepOutRequest'!");
        }

        protected virtual void HandleTerminateRequestAsync(IRequestResponder<TerminateArguments> responder)
        {
            Protocol.OnRequestReceived("terminate", responder);
        }

        protected virtual TerminateResponse HandleTerminateRequest(TerminateArguments arguments)
        {
            throw new NotImplementedException("No handler implemented for request type 'TerminateRequest'!");
        }

        protected virtual void HandleTerminateThreadsRequestAsync(IRequestResponder<TerminateThreadsArguments> responder)
        {
            Protocol.OnRequestReceived("terminateThreads", responder);
        }

        protected virtual TerminateThreadsResponse HandleTerminateThreadsRequest(TerminateThreadsArguments arguments)
        {
            throw new NotImplementedException("No handler implemented for request type 'TerminateThreadsRequest'!");
        }

        protected virtual void HandleThreadsRequestAsync(IRequestResponder<ThreadsArguments, ThreadsResponse> responder)
        {
            Protocol.OnRequestReceived("threads", responder);
        }

        protected virtual ThreadsResponse HandleThreadsRequest(ThreadsArguments arguments)
        {
            throw new NotImplementedException("No handler implemented for request type 'ThreadsRequest'!");
        }

        protected virtual void HandleVariablesRequestAsync(IRequestResponder<VariablesArguments, VariablesResponse> responder)
        {
            Protocol.OnRequestReceived("variables", responder);
        }

        protected virtual VariablesResponse HandleVariablesRequest(VariablesArguments arguments)
        {
            throw new NotImplementedException("No handler implemented for request type 'VariablesRequest'!");
        }
    }
}
