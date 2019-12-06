using Newtonsoft.Json;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
	public class GotoTargetsArguments : DebugRequestArguments
	{
		[JsonProperty("source")]
		public Source Source
		{
			get;
			set;
		}

		[JsonProperty("line")]
		public int Line
		{
			get;
			set;
		}

		[JsonProperty("column", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public int? Column
		{
			get;
			set;
		}
	}
}
