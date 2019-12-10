using Newtonsoft.Json;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
    public class Thread : DebugType, INamed
    {
        [JsonProperty("id")]
        public int Id
        {
            get;
            set;
        }

        [JsonProperty("name")]
        public string Name
        {
            get;
            set;
        }

        public Thread()
        {
        }

        public Thread(int id, string name)
        {
            Id = id;
            Name = name;
        }
    }
}
