using Newtonsoft.Json;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
    public class HandshakeRequest : DebugClientRequestWithResponse<HandshakeArguments, HandshakeResponse>
    {
        public const string RequestType = "handshake";

        [JsonIgnore]
        public string Value
        {
            get
            {
                return base.Args.Value;
            }
            set
            {
                base.Args.Value = value;
            }
        }

        public HandshakeRequest()
            : base("handshake")
        {
        }

        public HandshakeRequest(string value)
            : base("handshake")
        {
            base.Args.Value = value;
        }
    }
}
