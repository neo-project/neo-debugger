using Newtonsoft.Json;
using System;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
	public class ValueFormat : DebugType
	{
		[JsonProperty("hex", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public bool? Hex
		{
			get;
			set;
		}

		[JsonProperty("rawString", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public bool? RawString
		{
			get;
			set;
		}

		[Obsolete("This constructor contains optional parameters and is no longer supported.  Please use the constructor which includes only required parameters.")]
		public ValueFormat(bool? hex = null, bool? rawString = null)
		{
			Hex = hex;
			RawString = rawString;
		}

		public ValueFormat()
		{
		}
	}
}
