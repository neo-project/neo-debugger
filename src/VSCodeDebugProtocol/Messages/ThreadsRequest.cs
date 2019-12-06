namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
	public class ThreadsRequest : DebugRequestWithResponse<ThreadsArguments, ThreadsResponse>
	{
		public const string RequestType = "threads";

		public ThreadsRequest()
			: base("threads")
		{
		}
	}
}
