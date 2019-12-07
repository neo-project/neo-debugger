using Newtonsoft.Json;
using System.Globalization;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
    public sealed class ProtocolResponse : ProtocolMessage
    {
        [JsonProperty("request_seq")]
        public int RequestSeq
        {
            get;
            set;
        }

        [JsonProperty("success")]
        public bool Success
        {
            get;
            set;
        }

        [JsonProperty("command")]
        public string Command
        {
            get;
            set;
        }

        [JsonProperty("message", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string Message
        {
            get;
            set;
        }

        [JsonProperty("body", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public dynamic Body
        {
            get;
            set;
        }

        public ProtocolResponse()
            : base(TypeValue.Response)
        {
        }

        public ProtocolResponse(int requestSeq, string command)
            : this()
        {
            RequestSeq = requestSeq;
            Command = command;
        }

        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "R ({0}-{1})", new object[2]
            {
                Command,
                RequestSeq
            });
        }
    }
}
