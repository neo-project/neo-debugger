using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
	[Obsolete]
	public class Script : DebugType
	{
		[JsonProperty("id")]
		public int Id
		{
			get;
			set;
		}

		[JsonProperty("source")]
		public Source Source
		{
			get;
			set;
		}

		[JsonProperty("children", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public List<Script> Children
		{
			get;
			set;
		}

		[Obsolete("This constructor contains optional parameters and is no longer supported.  Please use the constructor which includes only required parameters.")]
		public Script(int id, Source source, List<Script> children = null)
		{
			Id = id;
			Source = source;
			Children = children;
		}

		public Script()
		{
			Children = new List<Script>();
		}

		public Script(int id, Source source)
		{
			Id = id;
			Source = source;
		}
	}
}
