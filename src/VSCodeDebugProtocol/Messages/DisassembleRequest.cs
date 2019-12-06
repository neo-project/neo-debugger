using Newtonsoft.Json;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
	public class DisassembleRequest : DebugRequestWithResponse<DisassembleArguments, DisassembleResponse>
	{
		public const string RequestType = "disassemble";

		[JsonIgnore]
		public string MemoryReference
		{
			get
			{
				return base.Args.MemoryReference;
			}
			set
			{
				base.Args.MemoryReference = value;
			}
		}

		[JsonIgnore]
		public int? Offset
		{
			get
			{
				return base.Args.Offset;
			}
			set
			{
				base.Args.Offset = value;
			}
		}

		[JsonIgnore]
		public int? InstructionOffset
		{
			get
			{
				return base.Args.InstructionOffset;
			}
			set
			{
				base.Args.InstructionOffset = value;
			}
		}

		[JsonIgnore]
		public int InstructionCount
		{
			get
			{
				return base.Args.InstructionCount;
			}
			set
			{
				base.Args.InstructionCount = value;
			}
		}

		[JsonIgnore]
		public bool? ResolveSymbols
		{
			get
			{
				return base.Args.ResolveSymbols;
			}
			set
			{
				base.Args.ResolveSymbols = value;
			}
		}

		public DisassembleRequest()
			: base("disassemble")
		{
		}

		public DisassembleRequest(string memoryReference, int instructionCount)
			: base("disassemble")
		{
			base.Args.MemoryReference = memoryReference;
			base.Args.InstructionCount = instructionCount;
		}
	}
}
