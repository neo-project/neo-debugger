using Newtonsoft.Json;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
    public class LoadSymbolsArguments : DebugRequestArguments
    {
        [JsonProperty("id")]
        public object Id
        {
            get;
            set;
        }
    }
}
