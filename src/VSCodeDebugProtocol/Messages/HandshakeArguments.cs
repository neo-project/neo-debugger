using Newtonsoft.Json;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
    public class HandshakeArguments : DebugRequestArguments
    {
        [JsonProperty("value")]
        public string Value
        {
            get;
            set;
        }
    }
}
