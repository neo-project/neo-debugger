using Newtonsoft.Json;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
    public class ScopesRequest : DebugRequestWithResponse<ScopesArguments, ScopesResponse>
    {
        public const string RequestType = "scopes";

        [JsonIgnore]
        public int FrameId
        {
            get
            {
                return base.Args.FrameId;
            }
            set
            {
                base.Args.FrameId = value;
            }
        }

        public ScopesRequest()
            : base("scopes")
        {
        }

        public ScopesRequest(int frameId)
            : base("scopes")
        {
            base.Args.FrameId = frameId;
        }
    }
}
