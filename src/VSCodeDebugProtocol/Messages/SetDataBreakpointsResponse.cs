using Newtonsoft.Json;
using System.Collections.Generic;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
    public class SetDataBreakpointsResponse : ResponseBody
    {
        [JsonProperty("breakpoints")]
        public List<Breakpoint> Breakpoints
        {
            get;
            set;
        }

        public SetDataBreakpointsResponse()
        {
            Breakpoints = new List<Breakpoint>();
        }

        public SetDataBreakpointsResponse(List<Breakpoint> breakpoints)
        {
            Breakpoints = breakpoints;
        }
    }
}
