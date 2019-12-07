using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using System;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Protocol
{
    internal interface IEventInfo
    {
        Type EventType
        {
            get;
        }

        void InvokeHandler(DebugEvent eventBody);
    }
}
