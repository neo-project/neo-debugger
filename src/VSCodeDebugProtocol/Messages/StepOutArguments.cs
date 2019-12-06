using Newtonsoft.Json;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
	public class StepOutArguments : DebugRequestArguments
	{
		[JsonProperty("threadId")]
		public int ThreadId
		{
			get;
			set;
		}
	}
}
