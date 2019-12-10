using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Serialization;
using Newtonsoft.Json;
using System;
using System.Runtime.Serialization;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
    public class StackFrame : DebugType, INamed
    {
        public enum PresentationHintValue
        {
            [EnumMember(Value = "normal")]
            Normal = 0,
            [EnumMember(Value = "label")]
            Label = 1,
            [EnumMember(Value = "subtle")]
            Subtle = 2,
            [DefaultEnumValue]
            Unknown = int.MaxValue
        }

        [JsonProperty("presentationHint", DefaultValueHandling = DefaultValueHandling.Ignore)]
        private NullableEnumValue<PresentationHintValue> _presentationHint = new NullableEnumValue<PresentationHintValue>();

        [JsonProperty("id")]
        public int Id
        {
            get;
            set;
        }

        [JsonProperty("name")]
        public string Name
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

        [JsonProperty("line")]
        public int Line
        {
            get;
            set;
        }

        [JsonProperty("column")]
        public int Column
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

        [JsonProperty("instructionPointerReference", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string InstructionPointerReference
        {
            get;
            set;
        }

        [JsonProperty("moduleId", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public object ModuleId
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

        [Obsolete("This constructor contains optional parameters and is no longer supported.  Please use the constructor which includes only required parameters.")]
        public StackFrame(int id, string name, int line, int column, Source source = null, int? endLine = null, int? endColumn = null, object moduleId = null, PresentationHintValue? presentationHint = null)
        {
            Id = id;
            Name = name;
            Source = source;
            Line = line;
            Column = column;
            EndLine = endLine;
            EndColumn = endColumn;
            ModuleId = moduleId;
            PresentationHint = presentationHint;
        }

        public StackFrame()
        {
        }

        public StackFrame(int id, string name, int line, int column)
        {
            Id = id;
            Name = name;
            Line = line;
            Column = column;
        }
    }
}
