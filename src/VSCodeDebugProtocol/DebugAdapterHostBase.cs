using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Protocol;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Utilities;
using System;
using System.IO;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol
{
	public abstract class DebugAdapterHostBase
	{
		public DebugProtocolHost Protocol
		{
			get;
			private set;
		}

		protected void InitializeProtocolHost(Stream debugAdapterStdIn, Stream debugAdapterStdOut)
		{
			InitializeProtocolHost(debugAdapterStdIn, debugAdapterStdOut, DebugProtocolOptions.None);
		}

		protected void InitializeProtocolHost(Stream debugAdapterStdIn, Stream debugAdapterStdOut, DebugProtocolOptions options)
		{
			if (Protocol != null)
			{
				throw new InvalidOperationException("ProtocolHost is already initialized!");
			}
			Protocol = new DebugProtocolHost(debugAdapterStdIn, debugAdapterStdOut, registerStandardHandlers: false, options);
			Protocol.DispatcherError += OnDispatcherError;
			Protocol.EventReceived += OnProtocolEventReceived;
			Protocol.RequestReceived += OnProtocolRequestReceived;
			Protocol.RegisterEventType(delegate(BreakpointEvent e)
			{
				Protocol.OnEventReceived("breakpoint", e);
			});
			Protocol.RegisterEventType(delegate(ContinuedEvent e)
			{
				Protocol.OnEventReceived("continued", e);
			});
			Protocol.RegisterEventType(delegate(ExitedEvent e)
			{
				Protocol.OnEventReceived("exited", e);
			});
			Protocol.RegisterEventType(delegate(HeartbeatEvent e)
			{
				Protocol.OnEventReceived("heartbeat", e);
			});
			Protocol.RegisterEventType(delegate(InitializedEvent e)
			{
				Protocol.OnEventReceived("initialized", e);
			});
			Protocol.RegisterEventType(delegate(LoadedSourceEvent e)
			{
				Protocol.OnEventReceived("loadedSource", e);
			});
			Protocol.RegisterEventType(delegate(ModuleEvent e)
			{
				Protocol.OnEventReceived("module", e);
			});
			Protocol.RegisterEventType(delegate(OutputEvent e)
			{
				Protocol.OnEventReceived("output", e);
			});
			Protocol.RegisterEventType(delegate(ProcessEvent e)
			{
				Protocol.OnEventReceived("process", e);
			});
#pragma warning disable CS0612 // Type or member is obsolete
			Protocol.RegisterEventType(delegate(ScriptEvent e)
#pragma warning restore CS0612 // Type or member is obsolete
			{
				Protocol.OnEventReceived("script", e);
			});
			Protocol.RegisterEventType(delegate(StoppedEvent e)
			{
				Protocol.OnEventReceived("stopped", e);
			});
			Protocol.RegisterEventType(delegate(TerminatedEvent e)
			{
				Protocol.OnEventReceived("terminated", e);
			});
			Protocol.RegisterEventType(delegate(ThreadEvent e)
			{
				Protocol.OnEventReceived("thread", e);
			});
			Protocol.RegisterClientRequestType<HandshakeRequest, HandshakeArguments, HandshakeResponse>(delegate(IRequestResponder<HandshakeArguments, HandshakeResponse> r)
			{
				HandleHandshakeRequestAsync(r);
			});
			Protocol.RegisterClientRequestType<RunInTerminalRequest, RunInTerminalArguments, RunInTerminalResponse>(delegate(IRequestResponder<RunInTerminalArguments, RunInTerminalResponse> r)
			{
				HandleRunInTerminalRequestAsync(r);
			});
		}

		private void OnDispatcherError(object sender, DispatcherErrorEventArgs e)
		{
			HandleProtocolError(e.Exception);
		}

		protected virtual void HandleProtocolError(Exception ex)
		{
		}

		private void OnProtocolEventReceived(object sender, EventReceivedEventArgs e)
		{
			HandleProtocolEvent(e.EventType, e.Body);
		}

		protected virtual void HandleProtocolEvent(string eventType, object eventBody)
		{
			switch (eventType)
			{
			case "breakpoint":
				HandleBreakpointEvent((BreakpointEvent)eventBody);
				break;
			case "continued":
				HandleContinuedEvent((ContinuedEvent)eventBody);
				break;
			case "exited":
				HandleExitedEvent((ExitedEvent)eventBody);
				break;
			case "heartbeat":
				HandleHeartbeatEvent((HeartbeatEvent)eventBody);
				break;
			case "initialized":
				HandleInitializedEvent((InitializedEvent)eventBody);
				break;
			case "loadedSource":
				HandleLoadedSourceEvent((LoadedSourceEvent)eventBody);
				break;
			case "module":
				HandleModuleEvent((ModuleEvent)eventBody);
				break;
			case "output":
				HandleOutputEvent((OutputEvent)eventBody);
				break;
			case "process":
				HandleProcessEvent((ProcessEvent)eventBody);
				break;
			case "script":
#pragma warning disable CS0612 // Type or member is obsolete
					HandleScriptEvent((ScriptEvent)eventBody);
#pragma warning restore CS0612 // Type or member is obsolete
					break;
			case "stopped":
				HandleStoppedEvent((StoppedEvent)eventBody);
				break;
			case "terminated":
				HandleTerminatedEvent((TerminatedEvent)eventBody);
				break;
			case "thread":
				HandleThreadEvent((ThreadEvent)eventBody);
				break;
			default:
				throw new InvalidOperationException("Unknown event type '{0}'!".FormatInvariantWithArgs(eventType));
			}
		}

		protected virtual void HandleBreakpointEvent(BreakpointEvent body)
		{
		}

		protected virtual void HandleContinuedEvent(ContinuedEvent body)
		{
		}

		protected virtual void HandleExitedEvent(ExitedEvent body)
		{
		}

		internal virtual void HandleHeartbeatEvent(HeartbeatEvent body)
		{
		}

		protected virtual void HandleInitializedEvent(InitializedEvent body)
		{
		}

		protected virtual void HandleLoadedSourceEvent(LoadedSourceEvent body)
		{
		}

		protected virtual void HandleModuleEvent(ModuleEvent body)
		{
		}

		protected virtual void HandleOutputEvent(OutputEvent body)
		{
		}

		protected virtual void HandleProcessEvent(ProcessEvent body)
		{
		}

		[Obsolete]
		protected virtual void HandleScriptEvent(ScriptEvent body)
		{
		}

		protected virtual void HandleStoppedEvent(StoppedEvent body)
		{
		}

		protected virtual void HandleTerminatedEvent(TerminatedEvent body)
		{
		}

		protected virtual void HandleThreadEvent(ThreadEvent body)
		{
		}

		private void OnProtocolRequestReceived(object sender, RequestReceivedEventArgs e)
		{
			e.Response = HandleProtocolRequest(e.Command, e.Args);
		}

		protected virtual ResponseBody HandleProtocolRequest(string requestType, object requestArgs)
		{
			if (!(requestType == "handshake"))
			{
				if (requestType == "runInTerminal")
				{
					return HandleRunInTerminalRequest((RunInTerminalArguments)requestArgs);
				}
				throw new InvalidOperationException("Unknown request type '{0}'!".FormatInvariantWithArgs(requestType));
			}
			return HandleHandshakeRequest((HandshakeArguments)requestArgs);
		}

		protected virtual void HandleHandshakeRequestAsync(IRequestResponder<HandshakeArguments, HandshakeResponse> responder)
		{
			Protocol.OnRequestReceived("handshake", responder);
		}

		protected virtual HandshakeResponse HandleHandshakeRequest(HandshakeArguments arguments)
		{
			throw new NotImplementedException("No handler implemented for request type 'HandshakeRequest'!");
		}

		protected virtual void HandleRunInTerminalRequestAsync(IRequestResponder<RunInTerminalArguments, RunInTerminalResponse> responder)
		{
			Protocol.OnRequestReceived("runInTerminal", responder);
		}

		protected virtual RunInTerminalResponse HandleRunInTerminalRequest(RunInTerminalArguments arguments)
		{
			throw new NotImplementedException("No handler implemented for request type 'RunInTerminalRequest'!");
		}
	}
}
