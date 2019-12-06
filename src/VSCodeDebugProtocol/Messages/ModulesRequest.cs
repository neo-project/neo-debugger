using Newtonsoft.Json;
using System;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
	public class ModulesRequest : DebugRequestWithResponse<ModulesArguments, ModulesResponse>
	{
		public const string RequestType = "modules";

		[JsonIgnore]
		public int? StartModule
		{
			get
			{
				return base.Args.StartModule;
			}
			set
			{
				base.Args.StartModule = value;
			}
		}

		[JsonIgnore]
		public int? ModuleCount
		{
			get
			{
				return base.Args.ModuleCount;
			}
			set
			{
				base.Args.ModuleCount = value;
			}
		}

		[Obsolete("This constructor contains optional parameters and is no longer supported.  Please use the constructor which includes only required parameters.")]
		public ModulesRequest(int? startModule = null, int? moduleCount = null)
			: base("modules")
		{
			base.Args.StartModule = startModule;
			base.Args.ModuleCount = moduleCount;
		}

		public ModulesRequest()
			: base("modules")
		{
		}
	}
}
