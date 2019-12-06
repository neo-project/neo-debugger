using Newtonsoft.Json;
using System.Collections.Generic;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
	public class TerminateThreadsArguments : DebugRequestArguments
	{
		[JsonProperty("threadIds", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public List<int> ThreadIds
		{
			get;
			set;
		}
	}
}
