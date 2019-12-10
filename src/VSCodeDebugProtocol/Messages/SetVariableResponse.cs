using Newtonsoft.Json;
using System;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
    public class SetVariableResponse : ResponseBody
    {
        [JsonProperty("value")]
        public string Value
        {
            get;
            set;
        }

        [JsonProperty("type", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string Type
        {
            get;
            set;
        }

        [JsonProperty("variablesReference", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public int? VariablesReference
        {
            get;
            set;
        }

        [JsonProperty("namedVariables", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public int? NamedVariables
        {
            get;
            set;
        }

        [JsonProperty("indexedVariables", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public int? IndexedVariables
        {
            get;
            set;
        }

        [Obsolete("This constructor contains optional parameters and is no longer supported.  Please use the constructor which includes only required parameters.")]
        public SetVariableResponse(string value, string type = null, int? variablesReference = null, int? namedVariables = null, int? indexedVariables = null)
        {
            Value = value;
            Type = type;
            VariablesReference = variablesReference;
            NamedVariables = namedVariables;
            IndexedVariables = indexedVariables;
        }

        public SetVariableResponse()
        {
        }

        public SetVariableResponse(string value)
        {
            Value = value;
        }
    }
}
