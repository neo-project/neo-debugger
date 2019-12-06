using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Serialization;
using Newtonsoft.Json;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
	public class DataBreakpoint : DebugType
	{
		[JsonProperty("accessType", DefaultValueHandling = DefaultValueHandling.Ignore)]
		private NullableEnumValue<DataBreakpointAccessType> _accessType = new NullableEnumValue<DataBreakpointAccessType>();

		[JsonProperty("dataId")]
		public string DataId
		{
			get;
			set;
		}

		[JsonIgnore]
		public DataBreakpointAccessType? AccessType
		{
			get
			{
				return _accessType.Value;
			}
			set
			{
				_accessType.Value = value;
			}
		}

		[JsonProperty("condition", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public string Condition
		{
			get;
			set;
		}

		[JsonProperty("hitCondition", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public string HitCondition
		{
			get;
			set;
		}

		public DataBreakpoint()
		{
		}

		public DataBreakpoint(string dataId)
		{
			DataId = dataId;
		}
	}
}
