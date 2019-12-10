using Newtonsoft.Json;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
    public class PauseRequest : DebugRequest<PauseArguments>
    {
        public const string RequestType = "pause";

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

        public PauseRequest()
            : base("pause")
        {
        }

        public PauseRequest(int threadId)
            : base("pause")
        {
            base.Args.ThreadId = threadId;
        }
    }
}
