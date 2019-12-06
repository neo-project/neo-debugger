using Newtonsoft.Json;
using System.Collections.Generic;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
	public class TerminateThreadsRequest : DebugRequest<TerminateThreadsArguments>
	{
		public const string RequestType = "terminateThreads";

		[JsonIgnore]
		public List<int> ThreadIds
		{
			get
			{
				return base.Args.ThreadIds;
			}
			set
			{
				base.Args.ThreadIds = value;
			}
		}

		public TerminateThreadsRequest()
			: base("terminateThreads")
		{
			base.Args.ThreadIds = new List<int>();
		}
	}
}
