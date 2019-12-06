using Newtonsoft.Json;
using System;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
	public class Module : DebugType, INamed
	{
		[JsonProperty("id")]
		public object Id
		{
			get;
			set;
		}

		[JsonProperty("name")]
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

		[JsonProperty("isOptimized", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public bool? IsOptimized
		{
			get;
			set;
		}

		[JsonProperty("isUserCode", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public bool? IsUserCode
		{
			get;
			set;
		}

		[JsonProperty("version", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public string Version
		{
			get;
			set;
		}

		[JsonProperty("symbolStatus", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public string SymbolStatus
		{
			get;
			set;
		}

		[JsonProperty("symbolFilePath", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public string SymbolFilePath
		{
			get;
			set;
		}

		[JsonProperty("dateTimeStamp", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public string DateTimeStamp
		{
			get;
			set;
		}

		[JsonProperty("addressRange", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public string AddressRange
		{
			get;
			set;
		}

		[JsonProperty("vsLoadAddress", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public string VsLoadAddress
		{
			get;
			set;
		}

		[JsonProperty("vsPreferredLoadAddress", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public string VsPreferredLoadAddress
		{
			get;
			set;
		}

		[JsonProperty("vsModuleSize", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public int? VsModuleSize
		{
			get;
			set;
		}

		[JsonProperty("vsLoadOrder", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public int? VsLoadOrder
		{
			get;
			set;
		}

		[JsonProperty("vsTimestampUTC", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public string VsTimestampUTC
		{
			get;
			set;
		}

		[JsonProperty("vsIs64Bit", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public bool? VsIs64Bit
		{
			get;
			set;
		}

		[JsonProperty("vsAppDomain", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public string VsAppDomain
		{
			get;
			set;
		}

		[JsonProperty("vsAppDomainId", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public int? VsAppDomainId
		{
			get;
			set;
		}

		[Obsolete("This constructor contains optional parameters and is no longer supported.  Please use the constructor which includes only required parameters.")]
		public Module(object id, string name, string path = null, bool? isOptimized = null, bool? isUserCode = null, string version = null, string symbolStatus = null, string symbolFilePath = null, string dateTimeStamp = null, string addressRange = null, string vsLoadAddress = null, string vsPreferredLoadAddress = null, int? vsModuleSize = null, int? vsLoadOrder = null, string vsTimestampUTC = null, bool? vsIs64Bit = null, string vsAppDomain = null, int? vsAppDomainId = null)
		{
			Id = id;
			Name = name;
			Path = path;
			IsOptimized = isOptimized;
			IsUserCode = isUserCode;
			Version = version;
			SymbolStatus = symbolStatus;
			SymbolFilePath = symbolFilePath;
			DateTimeStamp = dateTimeStamp;
			AddressRange = addressRange;
			VsLoadAddress = vsLoadAddress;
			VsPreferredLoadAddress = vsPreferredLoadAddress;
			VsModuleSize = vsModuleSize;
			VsLoadOrder = vsLoadOrder;
			VsTimestampUTC = vsTimestampUTC;
			VsIs64Bit = vsIs64Bit;
			VsAppDomain = vsAppDomain;
			VsAppDomainId = vsAppDomainId;
		}

		public Module()
		{
		}

		public Module(object id, string name)
		{
			Id = id;
			Name = name;
		}
	}
}
