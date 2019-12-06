using Newtonsoft.Json;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
	public class ReadMemoryRequest : DebugRequestWithResponse<ReadMemoryArguments, ReadMemoryResponse>
	{
		public const string RequestType = "readMemory";

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
		public int Count
		{
			get
			{
				return base.Args.Count;
			}
			set
			{
				base.Args.Count = value;
			}
		}

		public ReadMemoryRequest()
			: base("readMemory")
		{
		}

		public ReadMemoryRequest(string memoryReference, int count)
			: base("readMemory")
		{
			base.Args.MemoryReference = memoryReference;
			base.Args.Count = count;
		}
	}
}
