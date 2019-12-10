using Newtonsoft.Json;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
    public class StepInTargetsArguments : DebugRequestArguments
    {
        [JsonProperty("frameId")]
        public int FrameId
        {
            get;
            set;
        }
    }
}
