using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
	public class ModulesResponse : ResponseBody
	{
		[JsonProperty("modules")]
		public List<Module> Modules
		{
			get;
			set;
		}

		[JsonProperty("totalModules", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public int? TotalModules
		{
			get;
			set;
		}

		[Obsolete("This constructor contains optional parameters and is no longer supported.  Please use the constructor which includes only required parameters.")]
		public ModulesResponse(List<Module> modules, int? totalModules = null)
		{
			Modules = modules;
			TotalModules = totalModules;
		}

		public ModulesResponse()
		{
			Modules = new List<Module>();
		}

		public ModulesResponse(List<Module> modules)
		{
			Modules = modules;
		}
	}
}
