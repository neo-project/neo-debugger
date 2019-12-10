using Newtonsoft.Json;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
    public class SourceArguments : DebugRequestArguments
    {
        [JsonProperty("source", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public Source Source
        {
            get;
            set;
        }

        [JsonProperty("sourceReference")]
        public int SourceReference
        {
            get;
            set;
        }

        [JsonProperty("preferredEncoding", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string PreferredEncoding
        {
            get;
            set;
        }
    }
}
