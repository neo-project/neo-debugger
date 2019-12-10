using Newtonsoft.Json;
using System.Collections.Generic;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
    public class GotoTargetsResponse : ResponseBody
    {
        [JsonProperty("targets")]
        public List<GotoTarget> Targets
        {
            get;
            set;
        }

        public GotoTargetsResponse()
        {
            Targets = new List<GotoTarget>();
        }

        public GotoTargetsResponse(List<GotoTarget> targets)
        {
            Targets = targets;
        }
    }
}
