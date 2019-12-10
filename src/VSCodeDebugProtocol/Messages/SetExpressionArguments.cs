using Newtonsoft.Json;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
    public class SetExpressionArguments : DebugRequestArguments
    {
        [JsonProperty("expression")]
        public string Expression
        {
            get;
            set;
        }

        [JsonProperty("value")]
        public string Value
        {
            get;
            set;
        }

        [JsonProperty("frameId", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public int? FrameId
        {
            get;
            set;
        }

        [JsonProperty("format", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public ValueFormat Format
        {
            get;
            set;
        }

        [JsonProperty("timeout", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public int? Timeout
        {
            get;
            set;
        }
    }
}
