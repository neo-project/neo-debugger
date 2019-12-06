using Newtonsoft.Json;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
	public class StepInArguments : DebugRequestArguments
	{
		[JsonProperty("threadId")]
		public int ThreadId
		{
			get;
			set;
		}

		[JsonProperty("targetId", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public int? TargetId
		{
			get;
			set;
		}
	}
}
