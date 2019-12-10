using Newtonsoft.Json;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
    public class ContinueRequest : DebugRequestWithResponse<ContinueArguments, ContinueResponse>
    {
        public const string RequestType = "continue";

        [JsonIgnore]
        public int ThreadId
        {
            get
            {
                return base.Args.ThreadId;
            }
            set
            {
                base.Args.ThreadId = value;
            }
        }

        public ContinueRequest()
            : base("continue")
        {
        }

        public ContinueRequest(int threadId)
            : base("continue")
        {
            base.Args.ThreadId = threadId;
        }
    }
}
