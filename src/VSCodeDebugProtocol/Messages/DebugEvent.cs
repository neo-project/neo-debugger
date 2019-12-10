using Newtonsoft.Json;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
    public abstract class DebugEvent : ProtocolObject
    {
        [JsonIgnore]
        public string Type
        {
            get;
            internal set;
        }

        protected DebugEvent(string type)
        {
            Type = type;
        }
    }
}
