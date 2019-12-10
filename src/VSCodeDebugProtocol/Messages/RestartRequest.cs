namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
    public class RestartRequest : DebugRequest<RestartArguments>
    {
        public const string RequestType = "restart";

        public RestartRequest()
            : base("restart")
        {
        }
    }
}
