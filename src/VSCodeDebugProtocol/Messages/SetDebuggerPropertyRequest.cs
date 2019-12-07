using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
    public class SetDebuggerPropertyRequest : DebugRequest<SetDebuggerPropertyArguments>
    {
        public const string RequestType = "setDebuggerProperty";

        [JsonIgnore]
        public Dictionary<string, JToken> DebuggerProperties
        {
            get
            {
                return base.Args.DebuggerProperties;
            }
            set
            {
                base.Args.DebuggerProperties = value;
            }
        }

        [Obsolete("This constructor contains optional parameters and is no longer supported.  Please use the constructor which includes only required parameters.")]
        public SetDebuggerPropertyRequest(Dictionary<string, JToken> debuggerProperties = null)
            : base("setDebuggerProperty")
        {
            base.Args.DebuggerProperties = debuggerProperties;
        }

        public SetDebuggerPropertyRequest()
            : base("setDebuggerProperty")
        {
        }
    }
}
