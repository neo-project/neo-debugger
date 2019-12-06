using Newtonsoft.Json;
using System.Collections.Generic;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
	public class ScopesResponse : ResponseBody
	{
		[JsonProperty("scopes")]
		public List<Scope> Scopes
		{
			get;
			set;
		}

		public ScopesResponse()
		{
			Scopes = new List<Scope>();
		}

		public ScopesResponse(List<Scope> scopes)
		{
			Scopes = scopes;
		}
	}
}
