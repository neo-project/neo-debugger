using Newtonsoft.Json;
using System;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
	public class ExceptionBreakpointsFilter : DebugType
	{
		[JsonProperty("filter")]
		public string Filter
		{
			get;
			set;
		}

		[JsonProperty("label")]
		public string Label
		{
			get;
			set;
		}

		[JsonProperty("default", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public bool? Default
		{
			get;
			set;
		}

		[Obsolete("This constructor contains optional parameters and is no longer supported.  Please use the constructor which includes only required parameters.")]
		public ExceptionBreakpointsFilter(string filter, string label, bool? @default = null)
		{
			Filter = filter;
			Label = label;
			Default = @default;
		}

		public ExceptionBreakpointsFilter()
		{
		}

		public ExceptionBreakpointsFilter(string filter, string label)
		{
			Filter = filter;
			Label = label;
		}
	}
}
