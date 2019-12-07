using Newtonsoft.Json;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
    public class DataBreakpointInfoRequest : DebugRequestWithResponse<DataBreakpointInfoArguments, DataBreakpointInfoResponse>
    {
        public const string RequestType = "dataBreakpointInfo";

        [JsonIgnore]
        public int? VariablesReference
        {
            get
            {
                return base.Args.VariablesReference;
            }
            set
            {
                base.Args.VariablesReference = value;
            }
        }

        [JsonIgnore]
        public string Name
        {
            get
            {
                return base.Args.Name;
            }
            set
            {
                base.Args.Name = value;
            }
        }

        public DataBreakpointInfoRequest()
            : base("dataBreakpointInfo")
        {
        }

        public DataBreakpointInfoRequest(string name)
            : base("dataBreakpointInfo")
        {
            base.Args.Name = name;
        }
    }
}
