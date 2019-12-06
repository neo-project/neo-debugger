using Newtonsoft.Json;
using System.Collections.Generic;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
	public class SetBreakpointsResponse : ResponseBody
	{
		[JsonProperty("breakpoints")]
		public List<Breakpoint> Breakpoints
		{
			get;
			set;
		}

		public SetBreakpointsResponse()
		{
			Breakpoints = new List<Breakpoint>();
		}

		public SetBreakpointsResponse(List<Breakpoint> breakpoints)
		{
			Breakpoints = breakpoints;
		}
	}
}
