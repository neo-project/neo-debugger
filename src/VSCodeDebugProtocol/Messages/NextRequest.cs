using Newtonsoft.Json;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
	public class NextRequest : DebugRequest<NextArguments>
	{
		public const string RequestType = "next";

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

		public NextRequest()
			: base("next")
		{
		}

		public NextRequest(int threadId)
			: base("next")
		{
			base.Args.ThreadId = threadId;
		}
	}
}
