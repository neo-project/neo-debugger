namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
    internal class HeartbeatEvent : DebugEvent
    {
        public const string EventType = "heartbeat";

        public HeartbeatEvent()
            : base("heartbeat")
        {
        }
    }
}
