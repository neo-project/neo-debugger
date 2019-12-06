using Newtonsoft.Json;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
	public class ExitedEvent : DebugEvent
	{
		public const string EventType = "exited";

		[JsonProperty("exitCode")]
		public int ExitCode
		{
			get;
			set;
		}

		public ExitedEvent()
			: base("exited")
		{
		}

		public ExitedEvent(int exitCode)
			: base("exited")
		{
			ExitCode = exitCode;
		}
	}
}
