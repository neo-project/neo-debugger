using Newtonsoft.Json;
using System.Collections.Generic;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
    public class LoadedSourcesResponse : ResponseBody
    {
        [JsonProperty("sources")]
        public List<Source> Sources
        {
            get;
            set;
        }

        public LoadedSourcesResponse()
        {
            Sources = new List<Source>();
        }

        public LoadedSourcesResponse(List<Source> sources)
        {
            Sources = sources;
        }
    }
}
