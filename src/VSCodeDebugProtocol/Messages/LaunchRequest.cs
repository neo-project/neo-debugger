using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
    public class LaunchRequest : DebugRequest<LaunchArguments>
    {
        public const string RequestType = "launch";

        [JsonIgnore]
        public bool? NoDebug
        {
            get
            {
                return base.Args.NoDebug;
            }
            set
            {
                base.Args.NoDebug = value;
            }
        }

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
        public LaunchRequest(bool? noDebug = null, Dictionary<string, JToken> configurationProperties = null)
            : base("launch")
        {
            base.Args.NoDebug = noDebug;
            base.Args.ConfigurationProperties = configurationProperties;
        }

        public LaunchRequest()
            : base("launch")
        {
        }
    }
}
