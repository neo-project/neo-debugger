using Newtonsoft.Json;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
	public class StepInTarget : DebugType
	{
		[JsonProperty("id")]
		public int Id
		{
			get;
			set;
		}

		[JsonProperty("label")]
		public string Label
		{
			get;
			set;
		}

		public StepInTarget()
		{
		}

		public StepInTarget(int id, string label)
		{
			Id = id;
			Label = label;
		}
	}
}
