using Newtonsoft.Json;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
	public class ModulesArguments : DebugRequestArguments
	{
		[JsonProperty("startModule", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public int? StartModule
		{
			get;
			set;
		}

		[JsonProperty("moduleCount", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public int? ModuleCount
		{
			get;
			set;
		}
	}
}
