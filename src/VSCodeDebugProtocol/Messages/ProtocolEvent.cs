using Newtonsoft.Json;
using System.Globalization;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
    public sealed class ProtocolEvent : ProtocolMessage
    {
        [JsonProperty("event")]
        public string EventType
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

        public ProtocolEvent()
            : base(TypeValue.Event)
        {
        }

        public ProtocolEvent(string eventType, dynamic body)
            : this()
        {
            EventType = eventType;
            Body = (object)body;
        }

        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "E ({0})", new object[1]
            {
                EventType
            });
        }
    }
}
