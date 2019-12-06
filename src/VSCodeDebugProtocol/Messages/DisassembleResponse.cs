using Newtonsoft.Json;
using System.Collections.Generic;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
	public class DisassembleResponse : ResponseBody
	{
		[JsonProperty("instructions")]
		public List<DisassembledInstruction> Instructions
		{
			get;
			set;
		}

		public DisassembleResponse()
		{
			Instructions = new List<DisassembledInstruction>();
		}

		public DisassembleResponse(List<DisassembledInstruction> instructions)
		{
			Instructions = instructions;
		}
	}
}
