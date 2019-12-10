using Newtonsoft.Json;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
    public class ModuleSymbolSearchLogResponse : ResponseBody
    {
        [JsonProperty("logText")]
        public string LogText
        {
            get;
            set;
        }

        public ModuleSymbolSearchLogResponse()
        {
        }

        public ModuleSymbolSearchLogResponse(string logText)
        {
            LogText = logText;
        }
    }
}
