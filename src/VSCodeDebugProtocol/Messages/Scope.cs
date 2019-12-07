using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Serialization;
using Newtonsoft.Json;
using System;
using System.Runtime.Serialization;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
    public class Scope : DebugType, INamed
    {
        public enum PresentationHintValue
        {
            [EnumMember(Value = "arguments")]
            Arguments = 0,
            [EnumMember(Value = "locals")]
            Locals = 1,
            [EnumMember(Value = "registers")]
            Registers = 2,
            [DefaultEnumValue]
            Unknown = int.MaxValue
        }

        [JsonProperty("presentationHint", DefaultValueHandling = DefaultValueHandling.Ignore)]
        private NullableEnumValue<PresentationHintValue> _presentationHint = new NullableEnumValue<PresentationHintValue>();

        [JsonProperty("name")]
        public string Name
        {
            get;
            set;
        }

        [JsonIgnore]
        public PresentationHintValue? PresentationHint
        {
            get
            {
                return _presentationHint.Value;
            }
            set
            {
                _presentationHint.Value = value;
            }
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

        [JsonProperty("expensive")]
        public bool Expensive
        {
            get;
            set;
        }

        [JsonProperty("source", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public Source Source
        {
            get;
            set;
        }

        [JsonProperty("line", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public int? Line
        {
            get;
            set;
        }

        [JsonProperty("column", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public int? Column
        {
            get;
            set;
        }

        [JsonProperty("endLine", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public int? EndLine
        {
            get;
            set;
        }

        [JsonProperty("endColumn", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public int? EndColumn
        {
            get;
            set;
        }

        [Obsolete("This constructor contains optional parameters and is no longer supported.  Please use the constructor which includes only required parameters.")]
        public Scope(string name, int variablesReference, bool expensive, int? namedVariables = null, int? indexedVariables = null, Source source = null, int? line = null, int? column = null, int? endLine = null, int? endColumn = null)
        {
            Name = name;
            VariablesReference = variablesReference;
            NamedVariables = namedVariables;
            IndexedVariables = indexedVariables;
            Expensive = expensive;
            Source = source;
            Line = line;
            Column = column;
            EndLine = endLine;
            EndColumn = endColumn;
        }

        public Scope()
        {
        }

        public Scope(string name, int variablesReference, bool expensive)
        {
            Name = name;
            VariablesReference = variablesReference;
            Expensive = expensive;
        }
    }
}
