using Newtonsoft.Json;
using System;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
    public class TerminatedEvent : DebugEvent
    {
        public const string EventType = "terminated";

        [JsonProperty("restart", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public object Restart
        {
            get;
            set;
        }

        [Obsolete("This constructor contains optional parameters and is no longer supported.  Please use the constructor which includes only required parameters.")]
        public TerminatedEvent(object restart = null)
            : base("terminated")
        {
            Restart = restart;
        }

        public TerminatedEvent()
            : base("terminated")
        {
        }
    }
}
