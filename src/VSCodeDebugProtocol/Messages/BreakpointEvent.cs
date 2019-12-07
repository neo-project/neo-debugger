using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Serialization;
using Newtonsoft.Json;
using System.Runtime.Serialization;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
    public class BreakpointEvent : DebugEvent
    {
        public enum ReasonValue
        {
            [EnumMember(Value = "changed")]
            Changed = 0,
            [EnumMember(Value = "new")]
            New = 1,
            [EnumMember(Value = "removed")]
            Removed = 2,
            [DefaultEnumValue]
            Unknown = int.MaxValue
        }

        public const string EventType = "breakpoint";

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

        [JsonProperty("breakpoint")]
        public Breakpoint Breakpoint
        {
            get;
            set;
        }

        public BreakpointEvent()
            : base("breakpoint")
        {
        }

        public BreakpointEvent(ReasonValue reason, Breakpoint breakpoint)
            : base("breakpoint")
        {
            Reason = reason;
            Breakpoint = breakpoint;
        }
    }
}
