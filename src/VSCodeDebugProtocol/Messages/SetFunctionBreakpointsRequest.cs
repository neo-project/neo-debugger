using Newtonsoft.Json;
using System.Collections.Generic;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
	public class SetFunctionBreakpointsRequest : DebugRequestWithResponse<SetFunctionBreakpointsArguments, SetFunctionBreakpointsResponse>
	{
		public const string RequestType = "setFunctionBreakpoints";

		[JsonIgnore]
		public List<FunctionBreakpoint> Breakpoints
		{
			get
			{
				return base.Args.Breakpoints;
			}
			set
			{
				base.Args.Breakpoints = value;
			}
		}

		public SetFunctionBreakpointsRequest()
			: base("setFunctionBreakpoints")
		{
			base.Args.Breakpoints = new List<FunctionBreakpoint>();
		}

		public SetFunctionBreakpointsRequest(List<FunctionBreakpoint> breakpoints)
			: base("setFunctionBreakpoints")
		{
			base.Args.Breakpoints = breakpoints;
		}
	}
}
