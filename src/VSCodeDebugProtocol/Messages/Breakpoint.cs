using Newtonsoft.Json;
using System;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
    public class Breakpoint : DebugType
    {
        [JsonProperty("id", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public int? Id
        {
            get;
            set;
        }

        [JsonProperty("verified")]
        public bool Verified
        {
            get;
            set;
        }

        [JsonProperty("message", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string Message
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
        public Breakpoint(bool verified, int? id = null, string message = null, Source source = null, int? line = null, int? column = null, int? endLine = null, int? endColumn = null)
        {
            Id = id;
            Verified = verified;
            Message = message;
            Source = source;
            Line = line;
            Column = column;
            EndLine = endLine;
            EndColumn = endColumn;
        }

        public Breakpoint()
        {
        }

        public Breakpoint(bool verified)
        {
            Verified = verified;
        }
    }
}
