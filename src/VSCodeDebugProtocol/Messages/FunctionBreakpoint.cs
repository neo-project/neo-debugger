using Newtonsoft.Json;
using System;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
	public class FunctionBreakpoint : DebugType, INamed
	{
		[JsonProperty("name")]
		public string Name
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

		[Obsolete("This constructor contains optional parameters and is no longer supported.  Please use the constructor which includes only required parameters.")]
		public FunctionBreakpoint(string name, string condition = null, string hitCondition = null)
		{
			Name = name;
			Condition = condition;
			HitCondition = hitCondition;
		}

		public FunctionBreakpoint()
		{
		}

		public FunctionBreakpoint(string name)
		{
			Name = name;
		}
	}
}
