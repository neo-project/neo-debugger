using Newtonsoft.Json;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
	public class DisassembledInstruction : DebugType
	{
		[JsonProperty("address")]
		public string Address
		{
			get;
			set;
		}

		[JsonProperty("instructionBytes", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public string InstructionBytes
		{
			get;
			set;
		}

		[JsonProperty("instruction")]
		public string Instruction
		{
			get;
			set;
		}

		[JsonProperty("symbol", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public string Symbol
		{
			get;
			set;
		}

		[JsonProperty("location", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public Source Location
		{
			get;
			set;
		}

		[JsonProperty("line", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public int? Line
		{
			get;
			set;
		}

		[JsonProperty("column", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public int? Column
		{
			get;
			set;
		}

		[JsonProperty("endLine", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public int? EndLine
		{
			get;
			set;
		}

		[JsonProperty("endColumn", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public int? EndColumn
		{
			get;
			set;
		}

		public DisassembledInstruction()
		{
		}

		public DisassembledInstruction(string address, string instruction)
		{
			Address = address;
			Instruction = instruction;
		}
	}
}
