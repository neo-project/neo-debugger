using Newtonsoft.Json;
using System;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
    public class ContinuedEvent : DebugEvent
    {
        public const string EventType = "continued";

        [JsonProperty("threadId")]
        public int ThreadId
        {
            get;
            set;
        }

        [JsonProperty("allThreadsContinued", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public bool? AllThreadsContinued
        {
            get;
            set;
        }

        [Obsolete("This constructor contains optional parameters and is no longer supported.  Please use the constructor which includes only required parameters.")]
        public ContinuedEvent(int threadId, bool? allThreadsContinued = null)
            : base("continued")
        {
            ThreadId = threadId;
            AllThreadsContinued = allThreadsContinued;
        }

        public ContinuedEvent()
            : base("continued")
        {
        }

        public ContinuedEvent(int threadId)
            : base("continued")
        {
            ThreadId = threadId;
        }
    }
}
