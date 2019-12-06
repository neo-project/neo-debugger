using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
	public class LaunchArguments : DebugRequestArguments
	{
		[JsonProperty("noDebug", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public bool? NoDebug
		{
			get;
			set;
		}

		[JsonProperty("__restart", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public object _Restart
		{
			get;
			set;
		}

		[JsonExtensionData(ReadData = true, WriteData = true)]
		public Dictionary<string, JToken> ConfigurationProperties
		{
			get
			{
				return base.AdditionalProperties;
			}
			set
			{
				base.AdditionalProperties = value;
			}
		}
	}
}
