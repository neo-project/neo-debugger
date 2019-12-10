using Newtonsoft.Json;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
    public class StepBackRequest : DebugRequest<StepBackArguments>
    {
        public const string RequestType = "stepBack";

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

        public StepBackRequest()
            : base("stepBack")
        {
        }

        public StepBackRequest(int threadId)
            : base("stepBack")
        {
            base.Args.ThreadId = threadId;
        }
    }
}
