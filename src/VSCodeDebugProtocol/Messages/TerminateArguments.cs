using Newtonsoft.Json;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
	public class TerminateArguments : DebugRequestArguments
	{
		[JsonProperty("restart", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public bool? Restart
		{
			get;
			set;
		}
	}
}
