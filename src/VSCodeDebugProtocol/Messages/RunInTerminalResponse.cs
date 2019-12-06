using Newtonsoft.Json;
using System;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
	public class RunInTerminalResponse : ResponseBody
	{
		[JsonProperty("processId", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public int? ProcessId
		{
			get;
			set;
		}

		[JsonProperty("shellProcessId", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public int? ShellProcessId
		{
			get;
			set;
		}

		[Obsolete("This constructor contains optional parameters and is no longer supported.  Please use the constructor which includes only required parameters.")]
		public RunInTerminalResponse(int? processId = null)
		{
			ProcessId = processId;
		}

		public RunInTerminalResponse()
		{
		}
	}
}
