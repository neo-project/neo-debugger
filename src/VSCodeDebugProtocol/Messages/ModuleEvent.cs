using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Serialization;
using Newtonsoft.Json;
using System;
using System.Runtime.Serialization;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
	public class ModuleEvent : DebugEvent
	{
		public enum ReasonValue
		{
			[EnumMember(Value = "new")]
			New = 0,
			[EnumMember(Value = "changed")]
			Changed = 1,
			[EnumMember(Value = "removed")]
			Removed = 2,
			[DefaultEnumValue]
			Unknown = int.MaxValue
		}

		public const string EventType = "module";

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

		[JsonProperty("module")]
		public Module Module
		{
			get;
			set;
		}

		[JsonProperty("vsMessage", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public string VsMessage
		{
			get;
			set;
		}

		[Obsolete("This constructor contains optional parameters and is no longer supported.  Please use the constructor which includes only required parameters.")]
		public ModuleEvent(ReasonValue reason, Module module, string vsMessage = null)
			: base("module")
		{
			Reason = reason;
			Module = module;
			VsMessage = vsMessage;
		}

		public ModuleEvent()
			: base("module")
		{
		}

		public ModuleEvent(ReasonValue reason, Module module)
			: base("module")
		{
			Reason = reason;
			Module = module;
		}
	}
}
