using Newtonsoft.Json;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
    public class SetSymbolOptionsArguments : DebugRequestArguments
    {
        [JsonProperty("symbolOptions")]
        public SymbolOptions SymbolOptions
        {
            get;
            set;
        }
    }
}
