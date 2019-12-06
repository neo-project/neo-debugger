using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Serialization;
using Newtonsoft.Json;
using System.Runtime.Serialization;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
	public class LoadedSourceEvent : DebugEvent
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

		public const string EventType = "loadedSource";

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

		[JsonProperty("source")]
		public Source Source
		{
			get;
			set;
		}

		public LoadedSourceEvent()
			: base("loadedSource")
		{
		}

		public LoadedSourceEvent(ReasonValue reason, Source source)
			: base("loadedSource")
		{
			Reason = reason;
			Source = source;
		}
	}
}
