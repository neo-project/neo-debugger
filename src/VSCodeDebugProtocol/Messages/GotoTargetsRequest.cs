using Newtonsoft.Json;
using System;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
	public class GotoTargetsRequest : DebugRequestWithResponse<GotoTargetsArguments, GotoTargetsResponse>
	{
		public const string RequestType = "gotoTargets";

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
		public int Line
		{
			get
			{
				return base.Args.Line;
			}
			set
			{
				base.Args.Line = value;
			}
		}

		[JsonIgnore]
		public int? Column
		{
			get
			{
				return base.Args.Column;
			}
			set
			{
				base.Args.Column = value;
			}
		}

		[Obsolete("This constructor contains optional parameters and is no longer supported.  Please use the constructor which includes only required parameters.")]
		public GotoTargetsRequest(Source source, int line, int? column = null)
			: base("gotoTargets")
		{
			base.Args.Source = source;
			base.Args.Line = line;
			base.Args.Column = column;
		}

		public GotoTargetsRequest()
			: base("gotoTargets")
		{
		}

		public GotoTargetsRequest(Source source, int line)
			: base("gotoTargets")
		{
			base.Args.Source = source;
			base.Args.Line = line;
		}
	}
}
