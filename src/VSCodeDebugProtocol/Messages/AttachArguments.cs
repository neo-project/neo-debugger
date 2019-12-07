using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
    public class AttachArguments : DebugRequestArguments
    {
        [JsonProperty("__restart", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public object _Restart
        {
            get;
            set;
        }

        [JsonExtensionData(ReadData = true, WriteData = true)]
        public Dictionary<string, JToken> ConfigurationProperties
        {
            get
            {
                return base.AdditionalProperties;
            }
            set
            {
                base.AdditionalProperties = value;
            }
        }
    }
}
