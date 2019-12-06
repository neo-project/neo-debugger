using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
	public class SetBreakpointsRequest : DebugRequestWithResponse<SetBreakpointsArguments, SetBreakpointsResponse>
	{
		public const string RequestType = "setBreakpoints";

		[JsonIgnore]
		public Source Source
		{
			get
			{
				return base.Args.Source;
			}
			set
			{
				base.Args.Source = value;
			}
		}

		[JsonIgnore]
		public List<SourceBreakpoint> Breakpoints
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

		[JsonIgnore]
		public List<int> Lines
		{
			get
			{
				return base.Args.Lines;
			}
			set
			{
				base.Args.Lines = value;
			}
		}

		[JsonIgnore]
		public bool? SourceModified
		{
			get
			{
				return base.Args.SourceModified;
			}
			set
			{
				base.Args.SourceModified = value;
			}
		}

		[Obsolete("This constructor contains optional parameters and is no longer supported.  Please use the constructor which includes only required parameters.")]
		public SetBreakpointsRequest(Source source, List<SourceBreakpoint> breakpoints = null, List<int> lines = null, bool? sourceModified = null)
			: base("setBreakpoints")
		{
			base.Args.Source = source;
			base.Args.Breakpoints = breakpoints;
			base.Args.Lines = lines;
			base.Args.SourceModified = sourceModified;
		}

		public SetBreakpointsRequest()
			: base("setBreakpoints")
		{
			base.Args.Breakpoints = new List<SourceBreakpoint>();
			base.Args.Lines = new List<int>();
		}

		public SetBreakpointsRequest(Source source)
			: base("setBreakpoints")
		{
			base.Args.Source = source;
		}
	}
}
