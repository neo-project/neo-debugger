using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using System;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Protocol
{
    internal interface IRequestInfo
    {
        Type ArgsType
        {
            get;
        }

        void InvokeHandler(DebugProtocol protocol, ProtocolRequest request, object args);
    }
}
