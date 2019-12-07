using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Protocol;
using System;
using System.IO;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol
{
    public class DebugProtocolHost : DebugProtocol
    {
        public event EventHandler<EventReceivedEventArgs> EventReceived;

        public DebugProtocolHost(Stream debugAdapterStdIn, Stream debugAdapterStdOut)
            : this(debugAdapterStdIn, debugAdapterStdOut, registerStandardHandlers: true, DebugProtocolOptions.None)
        {
        }

        public DebugProtocolHost(Stream debugAdapterStdIn, Stream debugAdapterStdOut, bool registerStandardHandlers)
            : this(debugAdapterStdIn, debugAdapterStdOut, registerStandardHandlers, DebugProtocolOptions.None)
        {
        }

        public DebugProtocolHost(Stream debugAdapterStdIn, Stream debugAdapterStdOut, bool registerStandardHandlers, DebugProtocolOptions options)
            : base(debugAdapterStdOut, debugAdapterStdIn, options)
        {
            if (registerStandardHandlers)
            {
                RegisterStandardHandlers();
            }
        }

        private void RegisterStandardHandlers()
        {
            RegisterEventType(delegate (BreakpointEvent e)
            {
                OnEventReceived("breakpoint", e);
            });
            RegisterEventType(delegate (ContinuedEvent e)
            {
                OnEventReceived("continued", e);
            });
            RegisterEventType(delegate (ExitedEvent e)
            {
                OnEventReceived("exited", e);
            });
            RegisterEventType(delegate (HeartbeatEvent e)
            {
                OnEventReceived("heartbeat", e);
            });
            RegisterEventType(delegate (InitializedEvent e)
            {
                OnEventReceived("initialized", e);
            });
            RegisterEventType(delegate (LoadedSourceEvent e)
            {
                OnEventReceived("loadedSource", e);
            });
            RegisterEventType(delegate (ModuleEvent e)
            {
                OnEventReceived("module", e);
            });
            RegisterEventType(delegate (OutputEvent e)
            {
                OnEventReceived("output", e);
            });
            RegisterEventType(delegate (ProcessEvent e)
            {
                OnEventReceived("process", e);
            });
#pragma warning disable CS0612 // Type or member is obsolete
            RegisterEventType(delegate (ScriptEvent e)
#pragma warning restore CS0612 // Type or member is obsolete
            {
                OnEventReceived("script", e);
            });
            RegisterEventType(delegate (StoppedEvent e)
            {
                OnEventReceived("stopped", e);
            });
            RegisterEventType(delegate (TerminatedEvent e)
            {
                OnEventReceived("terminated", e);
            });
            RegisterEventType(delegate (ThreadEvent e)
            {
                OnEventReceived("thread", e);
            });
            RegisterClientRequestType<HandshakeRequest, HandshakeArguments, HandshakeResponse>(delegate (IRequestResponder<HandshakeArguments, HandshakeResponse> r)
            {
                OnRequestReceived("handshake", r);
            });
            RegisterClientRequestType<RunInTerminalRequest, RunInTerminalArguments, RunInTerminalResponse>(delegate (IRequestResponder<RunInTerminalArguments, RunInTerminalResponse> r)
            {
                OnRequestReceived("runInTerminal", r);
            });
        }

        protected internal override void OnEventReceived(string eventType, DebugEvent body)
        {
            this.EventReceived?.Invoke(this, new EventReceivedEventArgs(eventType, body));
        }

        public void SendRequest<TArgs, TResponse>(DebugRequestWithResponse<TArgs, TResponse> request, Action<TArgs, TResponse> completionFunc, Action<TArgs, ProtocolException> errorFunc = null) where TArgs : class, new() where TResponse : ResponseBody
        {
            SendRequestCore(request, completionFunc, errorFunc);
        }

        public TResponse SendRequestSync<TArgs, TResponse>(DebugRequestWithResponse<TArgs, TResponse> request) where TArgs : class, new() where TResponse : ResponseBody
        {
            return SendRequestSyncCore(request);
        }

        public void SendRequest<TArgs>(DebugRequest<TArgs> request, Action<TArgs> completionFunc, Action<TArgs, ProtocolException> errorFunc = null) where TArgs : class, new()
        {
            SendRequestCore(request, completionFunc, errorFunc);
        }

        public void SendRequestSync<TArgs>(DebugRequest<TArgs> request) where TArgs : class, new()
        {
            SendRequestSyncCore(request);
        }

        public void RegisterEventType<TEvent>(Action<TEvent> handler) where TEvent : DebugEvent, new()
        {
            RegisterEventTypeCore(new TEvent().Type, new EventInfo<TEvent>(handler));
        }

        public void RegisterClientRequestType<TRequest, TArgs>(Action<IRequestResponder<TArgs>> handler) where TRequest : DebugClientRequest<TArgs>, new() where TArgs : class, new()
        {
            RegisterRequestTypeCore(new TRequest().Command, new RequestInfo<TArgs>(handler));
        }

        public void RegisterClientRequestType<TRequest, TArgs, TResponse>(Action<IRequestResponder<TArgs, TResponse>> handler) where TRequest : DebugClientRequestWithResponse<TArgs, TResponse>, new() where TArgs : class, new() where TResponse : ResponseBody
        {
            RegisterRequestTypeCore(new TRequest().Command, new RequestWithResponseInfo<TArgs, TResponse>(handler));
        }
    }
}
