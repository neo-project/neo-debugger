using Newtonsoft.Json;
using System;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
    public class Variable : DebugType, INamed
    {
        [JsonProperty("name")]
        public string Name
        {
            get;
            set;
        }

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

        [JsonProperty("evaluateName", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string EvaluateName
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
        public Variable(string name, string value, int variablesReference, string type = null, VariablePresentationHint presentationHint = null, string evaluateName = null, int? namedVariables = null, int? indexedVariables = null)
        {
            Name = name;
            Value = value;
            Type = type;
            PresentationHint = presentationHint;
            EvaluateName = evaluateName;
            VariablesReference = variablesReference;
            NamedVariables = namedVariables;
            IndexedVariables = indexedVariables;
        }

        public Variable()
        {
        }

        public Variable(string name, string value, int variablesReference)
        {
            Name = name;
            Value = value;
            VariablesReference = variablesReference;
        }
    }
}
