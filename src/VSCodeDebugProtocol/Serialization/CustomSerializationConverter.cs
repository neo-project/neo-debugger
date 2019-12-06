using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Protocol;
using Newtonsoft.Json;
using System;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Serialization
{
	internal class CustomSerializationConverter : JsonConverter
	{
		private DebugProtocol protocol;

		public CustomSerializationConverter(DebugProtocol protocol)
		{
			this.protocol = protocol;
		}

		public override bool CanConvert(Type objectType)
		{
			return typeof(IJsonSerializable).IsAssignableFrom(objectType);
		}

		public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
		{
			if (existingValue == null)
			{
				existingValue = Activator.CreateInstance(objectType);
			}
			return ((IJsonSerializable)existingValue).Deserialize(reader, serializer, protocol);
		}

		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
		{
			if (value == null)
			{
				writer.WriteNull();
			}
			else
			{
				((IJsonSerializable)value).Serialize(writer, serializer, protocol);
			}
		}
	}
}
