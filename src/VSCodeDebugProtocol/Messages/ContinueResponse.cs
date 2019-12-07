using Newtonsoft.Json;
using System;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
    public class ContinueResponse : ResponseBody
    {
        [JsonProperty("allThreadsContinued", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public bool? AllThreadsContinued
        {
            get;
            set;
        }

        [Obsolete("This constructor contains optional parameters and is no longer supported.  Please use the constructor which includes only required parameters.")]
        public ContinueResponse(bool? allThreadsContinued = null)
        {
            AllThreadsContinued = allThreadsContinued;
        }

        public ContinueResponse()
        {
        }
    }
}
