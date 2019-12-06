using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Serialization;
using Newtonsoft.Json;
using System.Runtime.Serialization;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
	public class VariablesArguments : DebugRequestArguments
	{
		public enum FilterValue
		{
			[EnumMember(Value = "indexed")]
			Indexed = 0,
			[EnumMember(Value = "named")]
			Named = 1,
			[DefaultEnumValue]
			Unknown = int.MaxValue
		}

		[JsonProperty("filter", DefaultValueHandling = DefaultValueHandling.Ignore)]
		private NullableEnumValue<FilterValue> _filter = new NullableEnumValue<FilterValue>();

		[JsonProperty("variablesReference")]
		public int VariablesReference
		{
			get;
			set;
		}

		[JsonIgnore]
		public FilterValue? Filter
		{
			get
			{
				return _filter.Value;
			}
			set
			{
				_filter.Value = value;
			}
		}

		[JsonProperty("start", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public int? Start
		{
			get;
			set;
		}

		[JsonProperty("count", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public int? Count
		{
			get;
			set;
		}

		[JsonProperty("format", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public ValueFormat Format
		{
			get;
			set;
		}

		[JsonProperty("timeout", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public int? Timeout
		{
			get;
			set;
		}
	}
}
