using Newtonsoft.Json;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
	public class GotoArguments : DebugRequestArguments
	{
		[JsonProperty("threadId")]
		public int ThreadId
		{
			get;
			set;
		}

		[JsonProperty("targetId")]
		public int TargetId
		{
			get;
			set;
		}
	}
}
