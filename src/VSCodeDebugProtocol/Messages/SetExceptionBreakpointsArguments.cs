using Newtonsoft.Json;
using System.Collections.Generic;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
    public class SetExceptionBreakpointsArguments : DebugRequestArguments
    {
        [JsonProperty("filters")]
        public List<string> Filters
        {
            get;
            set;
        }

        [JsonProperty("exceptionOptions", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public List<ExceptionOptions> ExceptionOptions
        {
            get;
            set;
        }
    }
}
