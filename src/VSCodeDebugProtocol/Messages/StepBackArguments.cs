using Newtonsoft.Json;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
	public class StepBackArguments : DebugRequestArguments
	{
		[JsonProperty("threadId")]
		public int ThreadId
		{
			get;
			set;
		}
	}
}
