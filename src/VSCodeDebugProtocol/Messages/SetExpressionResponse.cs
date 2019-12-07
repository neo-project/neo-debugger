using Newtonsoft.Json;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
    public class SetExpressionResponse : ResponseBody
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

        [JsonProperty("presentationHint", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public VariablePresentationHint PresentationHint
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

        public SetExpressionResponse(string value = null)
        {
            Value = (value ?? string.Empty);
        }

        public SetExpressionResponse()
        {
        }
    }
}
