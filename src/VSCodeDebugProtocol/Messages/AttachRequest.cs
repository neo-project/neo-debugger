using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
    public class AttachRequest : DebugRequest<AttachArguments>
    {
        public const string RequestType = "attach";

        [JsonIgnore]
        public object _Restart
        {
            get
            {
                return base.Args._Restart;
            }
            set
            {
                base.Args._Restart = value;
            }
        }

        [JsonIgnore]
        public Dictionary<string, JToken> ConfigurationProperties
        {
            get
            {
                return base.Args.ConfigurationProperties;
            }
            set
            {
                base.Args.ConfigurationProperties = value;
            }
        }

        [Obsolete("This constructor contains optional parameters and is no longer supported.  Please use the constructor which includes only required parameters.")]
        public AttachRequest(Dictionary<string, JToken> configurationProperties = null)
            : base("attach")
        {
            base.Args.ConfigurationProperties = configurationProperties;
        }

        public AttachRequest()
            : base("attach")
        {
        }
    }
}
