using Newtonsoft.Json;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
	public class RestartFrameArguments : DebugRequestArguments
	{
		[JsonProperty("frameId")]
		public int FrameId
		{
			get;
			set;
		}
	}
}
