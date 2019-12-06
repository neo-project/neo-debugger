using Newtonsoft.Json;
using System.Collections.Generic;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
	public class SetDataBreakpointsArguments : DebugRequestArguments
	{
		[JsonProperty("breakpoints")]
		public List<DataBreakpoint> Breakpoints
		{
			get;
			set;
		}
	}
}
