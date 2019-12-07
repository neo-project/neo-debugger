using Newtonsoft.Json;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
    public class StepInTargetsRequest : DebugRequestWithResponse<StepInTargetsArguments, StepInTargetsResponse>
    {
        public const string RequestType = "stepInTargets";

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

        public StepInTargetsRequest()
            : base("stepInTargets")
        {
        }

        public StepInTargetsRequest(int frameId)
            : base("stepInTargets")
        {
            base.Args.FrameId = frameId;
        }
    }
}
