using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Serialization;
using Newtonsoft.Json;
using System;
using System.Runtime.Serialization;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
    public class ProcessEvent : DebugEvent
    {
        public enum StartMethodValue
        {
            [EnumMember(Value = "launch")]
            Launch = 0,
            [EnumMember(Value = "attach")]
            Attach = 1,
            [EnumMember(Value = "attachForSuspendedLaunch")]
            AttachForSuspendedLaunch = 2,
            [DefaultEnumValue]
            Unknown = int.MaxValue
        }

        public const string EventType = "process";

        [JsonProperty("startMethod", DefaultValueHandling = DefaultValueHandling.Ignore)]
        private NullableEnumValue<StartMethodValue> _startMethod = new NullableEnumValue<StartMethodValue>();

        [JsonProperty("name")]
        public string Name
        {
            get;
            set;
        }

        [JsonProperty("systemProcessId", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public int? SystemProcessId
        {
            get;
            set;
        }

        [JsonProperty("isLocalProcess", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public bool? IsLocalProcess
        {
            get;
            set;
        }

        [JsonIgnore]
        public StartMethodValue? StartMethod
        {
            get
            {
                return _startMethod.Value;
            }
            set
            {
                _startMethod.Value = value;
            }
        }

        [JsonProperty("pointerSize", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public int? PointerSize
        {
            get;
            set;
        }

        [Obsolete("This constructor contains optional parameters and is no longer supported.  Please use the constructor which includes only required parameters.")]
        public ProcessEvent(string name, int? systemProcessId = null, bool? isLocalProcess = null, StartMethodValue? startMethod = null)
            : base("process")
        {
            Name = name;
            SystemProcessId = systemProcessId;
            IsLocalProcess = isLocalProcess;
            StartMethod = startMethod;
        }

        public ProcessEvent()
            : base("process")
        {
        }

        public ProcessEvent(string name)
            : base("process")
        {
            Name = name;
        }
    }
}
