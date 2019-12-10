using Newtonsoft.Json;
using System.Collections.Generic;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
    public class SymbolOptions : DebugType
    {
        [JsonProperty("searchPaths", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public List<string> SearchPaths
        {
            get;
            set;
        }

        [JsonProperty("searchMicrosoftSymbolServer", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public bool? SearchMicrosoftSymbolServer
        {
            get;
            set;
        }

        [JsonProperty("searchNuGetOrgSymbolServer", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public bool? SearchNuGetOrgSymbolServer
        {
            get;
            set;
        }

        [JsonProperty("cachePath", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string CachePath
        {
            get;
            set;
        }

        [JsonProperty("moduleFilter", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public SymbolOptionsModuleFilter ModuleFilter
        {
            get;
            set;
        }

        public SymbolOptions()
        {
            SearchPaths = new List<string>();
        }
    }
}
