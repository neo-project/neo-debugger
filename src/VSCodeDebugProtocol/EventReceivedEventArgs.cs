using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using System;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol
{
    public class EventReceivedEventArgs : EventArgs
    {
        public string EventType
        {
            get;
            private set;
        }

        public DebugEvent Body
        {
            get;
            private set;
        }

        internal EventReceivedEventArgs(string eventType, DebugEvent body)
        {
            EventType = eventType;
            Body = body;
        }
    }
}
