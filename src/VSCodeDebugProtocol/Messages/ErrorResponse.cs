using Newtonsoft.Json;
using System;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
	public class ErrorResponse : ResponseBody
	{
		[JsonProperty("error", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public Message Error
		{
			get;
			set;
		}

		[Obsolete("This constructor contains optional parameters and is no longer supported.  Please use the constructor which includes only required parameters.")]
		public ErrorResponse(Message error = null)
		{
			Error = error;
		}

		public ErrorResponse()
		{
		}

		public ErrorResponse(ProtocolException ex)
		{
			Error = ex.ProtocolMessage;
		}
	}
}
