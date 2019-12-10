using Newtonsoft.Json;
using System.Collections.Generic;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
    public class SetFunctionBreakpointsResponse : ResponseBody
    {
        [JsonProperty("breakpoints")]
        public List<Breakpoint> Breakpoints
        {
            get;
            set;
        }

        public SetFunctionBreakpointsResponse()
        {
            Breakpoints = new List<Breakpoint>();
        }

        public SetFunctionBreakpointsResponse(List<Breakpoint> breakpoints)
        {
            Breakpoints = breakpoints;
        }
    }
}
