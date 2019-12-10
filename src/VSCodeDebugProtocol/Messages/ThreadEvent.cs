using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Serialization;
using Newtonsoft.Json;
using System.Runtime.Serialization;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
    public class ThreadEvent : DebugEvent
    {
        public enum ReasonValue
        {
            [EnumMember(Value = "started")]
            Started = 0,
            [EnumMember(Value = "exited")]
            Exited = 1,
            [DefaultEnumValue]
            Unknown = int.MaxValue
        }

        public const string EventType = "thread";

        [JsonProperty("reason")]
        private EnumValue<ReasonValue> _reason = new EnumValue<ReasonValue>();

        [JsonIgnore]
        public ReasonValue Reason
        {
            get
            {
                return _reason.Value;
            }
            set
            {
                _reason.Value = value;
            }
        }

        [JsonProperty("threadId")]
        public int ThreadId
        {
            get;
            set;
        }

        public ThreadEvent()
            : base("thread")
        {
        }

        public ThreadEvent(ReasonValue reason, int threadId)
            : base("thread")
        {
            Reason = reason;
            ThreadId = threadId;
        }
    }
}
