using Newtonsoft.Json;
using System.Globalization;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
    public sealed class ProtocolRequest : ProtocolMessage
    {
        [JsonProperty("command")]
        public string Command
        {
            get;
            set;
        }

        [JsonProperty("arguments", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public dynamic Arguments
        {
            get;
            set;
        }

        public ProtocolRequest()
            : base(TypeValue.Request)
        {
        }

        public ProtocolRequest(int seq, string command, dynamic args)
            : this()
        {
            base.Seq = seq;
            Command = command;
            Arguments = (object)args;
        }

        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "C ({0}-{1})", new object[2]
            {
                Command,
                base.Seq
            });
        }
    }
}
