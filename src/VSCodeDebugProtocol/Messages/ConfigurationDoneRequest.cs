namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
    public class ConfigurationDoneRequest : DebugRequest<ConfigurationDoneArguments>
    {
        public const string RequestType = "configurationDone";

        public ConfigurationDoneRequest()
            : base("configurationDone")
        {
        }
    }
}
