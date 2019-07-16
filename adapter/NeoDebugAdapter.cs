using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using System.IO;

namespace Neo.DebugAdapter
{
    internal class NeoDebugAdapter : DebugAdapterBase
    {
        public NeoDebugAdapter(Stream @in, Stream @out)
        {
            var p = this.Protocol;

            InitializeProtocolClient(@in, @out);

            var q = this.Protocol;
            ;
        }

        public void Run()
        {
            Protocol.Run();
        }

        protected override InitializeResponse HandleInitializeRequest(InitializeArguments arguments)
        {
            this.Protocol.SendEvent(new InitializedEvent());
            return base.HandleInitializeRequest(arguments);
        }

        protected override LaunchResponse HandleLaunchRequest(LaunchArguments arguments)
        {
            return base.HandleLaunchRequest(arguments);
        }
    }
}
