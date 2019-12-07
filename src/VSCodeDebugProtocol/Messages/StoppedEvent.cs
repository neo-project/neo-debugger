using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Serialization;
using Newtonsoft.Json;
using System;
using System.Runtime.Serialization;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
    public class StoppedEvent : DebugEvent
    {
        public enum ReasonValue
        {
            [EnumMember(Value = "step")]
            Step = 0,
            [EnumMember(Value = "breakpoint")]
            Breakpoint = 1,
            [EnumMember(Value = "exception")]
            Exception = 2,
            [EnumMember(Value = "pause")]
            Pause = 3,
            [EnumMember(Value = "entry")]
            Entry = 4,
            [EnumMember(Value = "goto")]
            Goto = 6,
            [EnumMember(Value = "function breakpoint")]
            FunctionBreakpoint = 7,
            [EnumMember(Value = "data breakpoint")]
            DataBreakpoint = 8,
            [EnumMember(Value = "restart")]
            Restart = 5,
            [DefaultEnumValue]
            Unknown = int.MaxValue
        }

        public const string EventType = "stopped";

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

        [JsonProperty("description", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string Description
        {
            get;
            set;
        }

        [JsonProperty("threadId", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public int? ThreadId
        {
            get;
            set;
        }

        [JsonProperty("preserveFocusHint", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public bool? PreserveFocusHint
        {
            get;
            set;
        }

        [JsonProperty("text", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string Text
        {
            get;
            set;
        }

        [JsonProperty("allThreadsStopped", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public bool? AllThreadsStopped
        {
            get;
            set;
        }

        [Obsolete("This constructor contains optional parameters and is no longer supported.  Please use the constructor which includes only required parameters.")]
        public StoppedEvent(ReasonValue reason, string description = null, int? threadId = null, string text = null, bool? allThreadsStopped = null)
            : base("stopped")
        {
            Reason = reason;
            Description = description;
            ThreadId = threadId;
            Text = text;
            AllThreadsStopped = allThreadsStopped;
        }

        public StoppedEvent()
            : base("stopped")
        {
        }

        public StoppedEvent(ReasonValue reason)
            : base("stopped")
        {
            Reason = reason;
        }
    }
}
