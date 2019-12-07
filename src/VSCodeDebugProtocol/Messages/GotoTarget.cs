using Newtonsoft.Json;
using System;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
    public class GotoTarget : DebugType
    {
        [JsonProperty("id")]
        public int Id
        {
            get;
            set;
        }

        [JsonProperty("label")]
        public string Label
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

        [JsonProperty("instructionPointerReference", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string InstructionPointerReference
        {
            get;
            set;
        }

        [Obsolete("This constructor contains optional parameters and is no longer supported.  Please use the constructor which includes only required parameters.")]
        public GotoTarget(int id, string label, int line, int? column = null, int? endLine = null, int? endColumn = null)
        {
            Id = id;
            Label = label;
            Line = line;
            Column = column;
            EndLine = endLine;
            EndColumn = endColumn;
        }

        public GotoTarget()
        {
        }

        public GotoTarget(int id, string label, int line)
        {
            Id = id;
            Label = label;
            Line = line;
        }
    }
}
