using Newtonsoft.Json;
using System.Collections.Generic;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
    public class SetDataBreakpointsRequest : DebugRequestWithResponse<SetDataBreakpointsArguments, SetDataBreakpointsResponse>
    {
        public const string RequestType = "setDataBreakpoints";

        [JsonIgnore]
        public List<DataBreakpoint> Breakpoints
        {
            get
            {
                return base.Args.Breakpoints;
            }
            set
            {
                base.Args.Breakpoints = value;
            }
        }

        public SetDataBreakpointsRequest()
            : base("setDataBreakpoints")
        {
            base.Args.Breakpoints = new List<DataBreakpoint>();
        }

        public SetDataBreakpointsRequest(List<DataBreakpoint> breakpoints)
            : base("setDataBreakpoints")
        {
            base.Args.Breakpoints = breakpoints;
        }
    }
}
