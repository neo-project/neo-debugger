using Newtonsoft.Json;
using System.Collections.Generic;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
	public class VariablesResponse : ResponseBody
	{
		[JsonProperty("variables")]
		public List<Variable> Variables
		{
			get;
			set;
		}

		public VariablesResponse()
		{
			Variables = new List<Variable>();
		}

		public VariablesResponse(List<Variable> variables)
		{
			Variables = variables;
		}
	}
}
