using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
    public class StackTraceResponse : ResponseBody
    {
        [JsonProperty("stackFrames")]
        public List<StackFrame> StackFrames
        {
            get;
            set;
        }

        [JsonProperty("totalFrames", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public int? TotalFrames
        {
            get;
            set;
        }

        [Obsolete("This constructor contains optional parameters and is no longer supported.  Please use the constructor which includes only required parameters.")]
        public StackTraceResponse(List<StackFrame> stackFrames, int? totalFrames = null)
        {
            StackFrames = stackFrames;
            TotalFrames = totalFrames;
        }

        public StackTraceResponse()
        {
            StackFrames = new List<StackFrame>();
        }

        public StackTraceResponse(List<StackFrame> stackFrames)
        {
            StackFrames = stackFrames;
        }
    }
}
