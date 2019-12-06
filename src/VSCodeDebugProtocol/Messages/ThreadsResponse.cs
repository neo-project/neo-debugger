using Newtonsoft.Json;
using System.Collections.Generic;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
	public class ThreadsResponse : ResponseBody
	{
		[JsonProperty("threads")]
		public List<Thread> Threads
		{
			get;
			set;
		}

		public ThreadsResponse()
		{
			Threads = new List<Thread>();
		}

		public ThreadsResponse(List<Thread> threads)
		{
			Threads = threads;
		}
	}
}
