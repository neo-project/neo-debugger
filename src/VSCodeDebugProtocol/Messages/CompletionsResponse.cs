using Newtonsoft.Json;
using System.Collections.Generic;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
	public class CompletionsResponse : ResponseBody
	{
		[JsonProperty("targets")]
		public List<CompletionItem> Targets
		{
			get;
			set;
		}

		public CompletionsResponse()
		{
			Targets = new List<CompletionItem>();
		}

		public CompletionsResponse(List<CompletionItem> targets)
		{
			Targets = targets;
		}
	}
}
