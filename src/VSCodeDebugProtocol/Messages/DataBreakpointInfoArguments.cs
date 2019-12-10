using Newtonsoft.Json;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
    public class DataBreakpointInfoArguments : DebugRequestArguments
    {
        [JsonProperty("variablesReference", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public int? VariablesReference
        {
            get;
            set;
        }

        [JsonProperty("name")]
        public string Name
        {
            get;
            set;
        }
    }
}
