using Newtonsoft.Json;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
	public class StackTraceArguments : DebugRequestArguments
	{
		[JsonProperty("threadId")]
		public int ThreadId
		{
			get;
			set;
		}

		[JsonProperty("startFrame", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public int? StartFrame
		{
			get;
			set;
		}

		[JsonProperty("levels", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public int? Levels
		{
			get;
			set;
		}

		[JsonProperty("format", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public StackFrameFormat Format
		{
			get;
			set;
		}
	}
}
