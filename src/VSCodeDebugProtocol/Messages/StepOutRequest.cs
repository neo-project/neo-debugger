using Newtonsoft.Json;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
    public class StepOutRequest : DebugRequest<StepOutArguments>
    {
        public const string RequestType = "stepOut";

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

        public StepOutRequest()
            : base("stepOut")
        {
        }

        public StepOutRequest(int threadId)
            : base("stepOut")
        {
            base.Args.ThreadId = threadId;
        }
    }
}
