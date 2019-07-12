using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol;
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
    }
}
