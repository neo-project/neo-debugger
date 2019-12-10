using Newtonsoft.Json;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
    public class ReadMemoryArguments : DebugRequestArguments
    {
        [JsonProperty("memoryReference")]
        public string MemoryReference
        {
            get;
            set;
        }

        [JsonProperty("offset", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public int? Offset
        {
            get;
            set;
        }

        [JsonProperty("count")]
        public int Count
        {
            get;
            set;
        }
    }
}
