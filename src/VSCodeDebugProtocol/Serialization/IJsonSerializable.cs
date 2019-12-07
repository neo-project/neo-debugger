using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Protocol;
using Newtonsoft.Json;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Serialization
{
    internal interface IJsonSerializable
    {
        bool ShouldSerialize
        {
            get;
        }

        object Deserialize(JsonReader reader, JsonSerializer serializer, DebugProtocol protocol);

        void Serialize(JsonWriter writer, JsonSerializer serializer, DebugProtocol protocol);
    }
}
