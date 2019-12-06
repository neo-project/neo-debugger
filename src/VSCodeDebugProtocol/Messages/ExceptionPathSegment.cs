using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
	public class ExceptionPathSegment : DebugType
	{
		[JsonProperty("negate", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public bool? Negate
		{
			get;
			set;
		}

		[JsonProperty("names")]
		public List<string> Names
		{
			get;
			set;
		}

		[Obsolete("This constructor contains optional parameters and is no longer supported.  Please use the constructor which includes only required parameters.")]
		public ExceptionPathSegment(List<string> names, bool? negate = null)
		{
			Negate = negate;
			Names = names;
		}

		public ExceptionPathSegment()
		{
			Names = new List<string>();
		}

		public ExceptionPathSegment(List<string> names)
		{
			Names = names;
		}

		public ExceptionPathSegment(bool? negate, params string[] names)
			: this(names?.ToList())
		{
			Negate = negate;
		}
	}
}
