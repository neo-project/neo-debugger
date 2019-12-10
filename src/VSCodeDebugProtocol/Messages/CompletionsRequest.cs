using Newtonsoft.Json;
using System;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
    public class CompletionsRequest : DebugRequestWithResponse<CompletionsArguments, CompletionsResponse>
    {
        public const string RequestType = "completions";

        [JsonIgnore]
        public int? FrameId
        {
            get
            {
                return base.Args.FrameId;
            }
            set
            {
                base.Args.FrameId = value;
            }
        }

        [JsonIgnore]
        public string Text
        {
            get
            {
                return base.Args.Text;
            }
            set
            {
                base.Args.Text = value;
            }
        }

        [JsonIgnore]
        public int Column
        {
            get
            {
                return base.Args.Column;
            }
            set
            {
                base.Args.Column = value;
            }
        }

        [JsonIgnore]
        public int? Line
        {
            get
            {
                return base.Args.Line;
            }
            set
            {
                base.Args.Line = value;
            }
        }

        [Obsolete("This constructor contains optional parameters and is no longer supported.  Please use the constructor which includes only required parameters.")]
        public CompletionsRequest(string text, int column, int? frameId = null, int? line = null)
            : base("completions")
        {
            base.Args.FrameId = frameId;
            base.Args.Text = text;
            base.Args.Column = column;
            base.Args.Line = line;
        }

        public CompletionsRequest()
            : base("completions")
        {
        }

        public CompletionsRequest(string text, int column)
            : base("completions")
        {
            base.Args.Text = text;
            base.Args.Column = column;
        }
    }
}
