using Newtonsoft.Json;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
    public class HandshakeResponse : ResponseBody
    {
        [JsonProperty("signature")]
        public string Signature
        {
            get;
            set;
        }

        public HandshakeResponse()
        {
        }

        public HandshakeResponse(string signature)
        {
            Signature = signature;
        }
    }
}
