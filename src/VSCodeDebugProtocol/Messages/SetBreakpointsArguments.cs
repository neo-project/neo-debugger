using Newtonsoft.Json;
using System.Collections.Generic;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
	public class SetBreakpointsArguments : DebugRequestArguments
	{
		[JsonProperty("source")]
		public Source Source
		{
			get;
			set;
		}

		[JsonProperty("breakpoints", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public List<SourceBreakpoint> Breakpoints
		{
			get;
			set;
		}

		[JsonProperty("lines", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public List<int> Lines
		{
			get;
			set;
		}

		[JsonProperty("sourceModified", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public bool? SourceModified
		{
			get;
			set;
		}
	}
}
