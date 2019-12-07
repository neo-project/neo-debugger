using Newtonsoft.Json;
using System;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
    public class EvaluateResponse : ResponseBody
    {
        [JsonProperty("result")]
        public string Result
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

        [JsonProperty("variablesReference")]
        public int VariablesReference
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

        [JsonProperty("memoryReference", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string MemoryReference
        {
            get;
            set;
        }

        [Obsolete("This constructor contains optional parameters and is no longer supported.  Please use the constructor which includes only required parameters.")]
        public EvaluateResponse(string result, int variablesReference, string type = null, VariablePresentationHint presentationHint = null, int? namedVariables = null, int? indexedVariables = null)
        {
            Result = result;
            Type = type;
            PresentationHint = presentationHint;
            VariablesReference = variablesReference;
            NamedVariables = namedVariables;
            IndexedVariables = indexedVariables;
        }

        public EvaluateResponse()
        {
        }

        public EvaluateResponse(string result, int variablesReference)
        {
            Result = result;
            VariablesReference = variablesReference;
        }
    }
}
