using Newtonsoft.Json;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
	public class SetVariableArguments : DebugRequestArguments
	{
		[JsonProperty("variablesReference")]
		public int VariablesReference
		{
			get;
			set;
		}

		[JsonProperty("name")]
		public string Name
		{
			get;
			set;
		}

		[JsonProperty("value")]
		public string Value
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
