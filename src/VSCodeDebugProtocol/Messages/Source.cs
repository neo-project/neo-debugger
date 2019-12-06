using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Serialization;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
	public class Source : DebugType, INamed
	{
		public enum PresentationHintValue
		{
			[EnumMember(Value = "normal")]
			Normal = 0,
			[EnumMember(Value = "emphasize")]
			Emphasize = 1,
			[EnumMember(Value = "deemphasize")]
			Deemphasize = 2,
			[DefaultEnumValue]
			Unknown = int.MaxValue
		}

		[JsonProperty("presentationHint", DefaultValueHandling = DefaultValueHandling.Ignore)]
		private NullableEnumValue<PresentationHintValue> _presentationHint = new NullableEnumValue<PresentationHintValue>();

		[JsonProperty("name", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public string Name
		{
			get;
			set;
		}

		[JsonProperty("path", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public string Path
		{
			get;
			set;
		}

		[JsonProperty("sourceReference", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public int? SourceReference
		{
			get;
			set;
		}

		[JsonIgnore]
		public PresentationHintValue? PresentationHint
		{
			get
			{
				return _presentationHint.Value;
			}
			set
			{
				_presentationHint.Value = value;
			}
		}

		[JsonProperty("origin", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public string Origin
		{
			get;
			set;
		}

		[JsonProperty("sources", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public List<Source> Sources
		{
			get;
			set;
		}

		[JsonProperty("adapterData", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public object AdapterData
		{
			get;
			set;
		}

		[JsonProperty("checksums", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public List<Checksum> Checksums
		{
			get;
			set;
		}

		[JsonProperty("vsSourceLinkInfo", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public VSSourceLinkInfo VsSourceLinkInfo
		{
			get;
			set;
		}

		[JsonProperty("alternateSourceReference", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public int? AlternateSourceReference
		{
			get;
			set;
		}

		[Obsolete("This constructor contains optional parameters and is no longer supported.  Please use the constructor which includes only required parameters.")]
		public Source(string name = null, string path = null, int? sourceReference = null, PresentationHintValue? presentationHint = null, string origin = null, List<Source> sources = null, object adapterData = null, List<Checksum> checksums = null)
		{
			Name = name;
			Path = path;
			SourceReference = sourceReference;
			PresentationHint = presentationHint;
			Origin = origin;
			Sources = sources;
			AdapterData = adapterData;
			Checksums = checksums;
		}

		public Source()
		{
			Sources = new List<Source>();
			Checksums = new List<Checksum>();
		}
	}
}
