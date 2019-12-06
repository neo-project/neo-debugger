using Newtonsoft.Json;
using System;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
	public class SourceBreakpoint : DebugType
	{
		[JsonProperty("line")]
		public int Line
		{
			get;
			set;
		}

		[JsonProperty("column", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public int? Column
		{
			get;
			set;
		}

		[JsonProperty("condition", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public string Condition
		{
			get;
			set;
		}

		[JsonProperty("hitCondition", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public string HitCondition
		{
			get;
			set;
		}

		[JsonProperty("logMessage", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public string LogMessage
		{
			get;
			set;
		}

		[Obsolete("This constructor contains optional parameters and is no longer supported.  Please use the constructor which includes only required parameters.")]
		public SourceBreakpoint(int line, int? column = null, string condition = null, string hitCondition = null)
		{
			Line = line;
			Column = column;
			Condition = condition;
			HitCondition = hitCondition;
		}

		public SourceBreakpoint()
		{
		}

		public SourceBreakpoint(int line)
		{
			Line = line;
		}
	}
}
