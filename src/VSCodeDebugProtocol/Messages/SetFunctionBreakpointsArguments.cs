using Newtonsoft.Json;
using System.Collections.Generic;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
    public class SetFunctionBreakpointsArguments : DebugRequestArguments
    {
        [JsonProperty("breakpoints")]
        public List<FunctionBreakpoint> Breakpoints
        {
            get;
            set;
        }
    }
}
