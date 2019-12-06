using Newtonsoft.Json;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
	public class ReadMemoryResponse : ResponseBody
	{
		[JsonProperty("address")]
		public string Address
		{
			get;
			set;
		}

		[JsonProperty("unreadableBytes", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public int? UnreadableBytes
		{
			get;
			set;
		}

		[JsonProperty("data", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public string Data
		{
			get;
			set;
		}

		public ReadMemoryResponse()
		{
		}

		public ReadMemoryResponse(string address)
		{
			Address = address;
		}
	}
}
