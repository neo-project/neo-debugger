using Newtonsoft.Json;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
	public class ReverseContinueRequest : DebugRequest<ReverseContinueArguments>
	{
		public const string RequestType = "reverseContinue";

		[JsonIgnore]
		public int ThreadId
		{
			get
			{
				return base.Args.ThreadId;
			}
			set
			{
				base.Args.ThreadId = value;
			}
		}

		public ReverseContinueRequest()
			: base("reverseContinue")
		{
		}

		public ReverseContinueRequest(int threadId)
			: base("reverseContinue")
		{
			base.Args.ThreadId = threadId;
		}
	}
}
