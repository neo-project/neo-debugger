namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
    public class InitializedEvent : DebugEvent
    {
        public const string EventType = "initialized";

        public InitializedEvent()
            : base("initialized")
        {
        }
    }
}
