using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Serialization;
using Newtonsoft.Json;
using System;
using System.Runtime.Serialization;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
    [Obsolete]
    public class ScriptEvent : DebugEvent
    {
        public enum ReasonValue
        {
            [EnumMember(Value = "new")]
            New = 0,
            [EnumMember(Value = "removed")]
            Removed = 1,
            [DefaultEnumValue]
            Unknown = int.MaxValue
        }

        public const string EventType = "script";

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

        [JsonProperty("script")]
        public Script Script
        {
            get;
            set;
        }

        public ScriptEvent()
            : base("script")
        {
        }

        public ScriptEvent(ReasonValue reason, Script script)
            : base("script")
        {
            Reason = reason;
            Script = script;
        }
    }
}
