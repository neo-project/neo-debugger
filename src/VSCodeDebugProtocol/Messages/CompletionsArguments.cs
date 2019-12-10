using Newtonsoft.Json;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
    public class CompletionsArguments : DebugRequestArguments
    {
        [JsonProperty("frameId", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public int? FrameId
        {
            get;
            set;
        }

        [JsonProperty("text")]
        public string Text
        {
            get;
            set;
        }

        [JsonProperty("column")]
        public int Column
        {
            get;
            set;
        }

        [JsonProperty("line", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public int? Line
        {
            get;
            set;
        }
    }
}
