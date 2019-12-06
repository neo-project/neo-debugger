using Newtonsoft.Json;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
	public class RestartFrameRequest : DebugRequest<RestartFrameArguments>
	{
		public const string RequestType = "restartFrame";

		[JsonIgnore]
		public int FrameId
		{
			get
			{
				return base.Args.FrameId;
			}
			set
			{
				base.Args.FrameId = value;
			}
		}

		public RestartFrameRequest()
			: base("restartFrame")
		{
		}

		public RestartFrameRequest(int frameId)
			: base("restartFrame")
		{
			base.Args.FrameId = frameId;
		}
	}
}
