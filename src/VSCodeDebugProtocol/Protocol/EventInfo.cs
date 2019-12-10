using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using System;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Protocol
{
    internal class EventInfo<TEvent> : IEventInfo where TEvent : DebugEvent, new()
    {
        private Action<TEvent> handler;

        public Type EventType => typeof(TEvent);

        internal EventInfo(Action<TEvent> handler)
        {
            this.handler = handler;
        }

        public void InvokeHandler(DebugEvent eventBody)
        {
            handler((TEvent)eventBody);
        }
    }
}
