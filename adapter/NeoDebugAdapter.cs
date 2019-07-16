using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using System.IO;

namespace Neo.DebugAdapter
{
    internal class NeoDebugAdapter : DebugAdapterBase
    {
        public NeoDebugAdapter(Stream @in, Stream @out)
        {
            InitializeProtocolClient(@in, @out);
        }

        public void Run()
        {
            Protocol.Run();
        }

        protected override InitializeResponse HandleInitializeRequest(InitializeArguments arguments)
        {
            this.Protocol.SendEvent(new InitializedEvent());

            return new InitializeResponse();
        }

        protected override LaunchResponse HandleLaunchRequest(LaunchArguments arguments)
        {
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
            return new ThreadsResponse();
        }
    }
}
