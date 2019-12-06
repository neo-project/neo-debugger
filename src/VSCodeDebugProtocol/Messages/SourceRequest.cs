using Newtonsoft.Json;
using System;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
	public class SourceRequest : DebugRequestWithResponse<SourceArguments, SourceResponse>
	{
		public const string RequestType = "source";

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
		public int SourceReference
		{
			get
			{
				return base.Args.SourceReference;
			}
			set
			{
				base.Args.SourceReference = value;
			}
		}

		[JsonIgnore]
		public string PreferredEncoding
		{
			get
			{
				return base.Args.PreferredEncoding;
			}
			set
			{
				base.Args.PreferredEncoding = value;
			}
		}

		[Obsolete("This constructor contains optional parameters and is no longer supported.  Please use the constructor which includes only required parameters.")]
		public SourceRequest(int sourceReference, Source source = null)
			: base("source")
		{
			base.Args.Source = source;
			base.Args.SourceReference = sourceReference;
		}

		public SourceRequest()
			: base("source")
		{
		}

		public SourceRequest(int sourceReference)
			: base("source")
		{
			base.Args.SourceReference = sourceReference;
		}
	}
}
