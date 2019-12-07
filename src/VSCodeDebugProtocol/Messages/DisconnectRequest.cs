using Newtonsoft.Json;
using System;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
    public class DisconnectRequest : DebugRequest<DisconnectArguments>
    {
        public const string RequestType = "disconnect";

        [JsonIgnore]
        public bool? Restart
        {
            get
            {
                return base.Args.Restart;
            }
            set
            {
                base.Args.Restart = value;
            }
        }

        [JsonIgnore]
        public bool? TerminateDebuggee
        {
            get
            {
                return base.Args.TerminateDebuggee;
            }
            set
            {
                base.Args.TerminateDebuggee = value;
            }
        }

        [Obsolete("This constructor contains optional parameters and is no longer supported.  Please use the constructor which includes only required parameters.")]
        public DisconnectRequest(bool? terminateDebuggee = null)
            : base("disconnect")
        {
            base.Args.TerminateDebuggee = terminateDebuggee;
        }

        public DisconnectRequest()
            : base("disconnect")
        {
        }
    }
}
