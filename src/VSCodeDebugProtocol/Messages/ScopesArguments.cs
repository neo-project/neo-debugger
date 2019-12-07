using Newtonsoft.Json;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
    public class ScopesArguments : DebugRequestArguments
    {
        [JsonProperty("frameId")]
        public int FrameId
        {
            get;
            set;
        }
    }
}
