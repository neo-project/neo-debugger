using Newtonsoft.Json;
using System.Collections.Generic;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
    public class StepInTargetsResponse : ResponseBody
    {
        [JsonProperty("targets")]
        public List<StepInTarget> Targets
        {
            get;
            set;
        }

        public StepInTargetsResponse()
        {
            Targets = new List<StepInTarget>();
        }

        public StepInTargetsResponse(List<StepInTarget> targets)
        {
            Targets = targets;
        }
    }
}
