using Newtonsoft.Json;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
    public class DisassembleArguments : DebugRequestArguments
    {
        [JsonProperty("memoryReference")]
        public string MemoryReference
        {
            get;
            set;
        }

        [JsonProperty("offset", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public int? Offset
        {
            get;
            set;
        }

        [JsonProperty("instructionOffset", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public int? InstructionOffset
        {
            get;
            set;
        }

        [JsonProperty("instructionCount")]
        public int InstructionCount
        {
            get;
            set;
        }

        [JsonProperty("resolveSymbols", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public bool? ResolveSymbols
        {
            get;
            set;
        }
    }
}
