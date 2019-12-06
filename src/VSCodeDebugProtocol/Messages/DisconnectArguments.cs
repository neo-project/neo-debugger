using Newtonsoft.Json;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
	public class DisconnectArguments : DebugRequestArguments
	{
		[JsonProperty("restart", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public bool? Restart
		{
			get;
			set;
		}

		[JsonProperty("terminateDebuggee", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public bool? TerminateDebuggee
		{
			get;
			set;
		}
	}
}
