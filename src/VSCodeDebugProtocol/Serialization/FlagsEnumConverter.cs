using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Protocol;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Utilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Serialization
{
    internal class FlagsEnumConverter : JsonConverter
    {
        private StringEnumConverter enumConverter = new StringEnumConverter
        {
            AllowIntegerValues = false
        };

        private DebugProtocol protocol;

        public FlagsEnumConverter(DebugProtocol protocol)
        {
            this.protocol = protocol;
        }

        public override bool CanConvert(Type objectType)
        {
            return objectType.IsFlagsEnum();
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType != JsonToken.StartArray || !reader.Read())
            {
                return null;
            }
            List<Enum> list = new List<Enum>();
            while (reader.TokenType == JsonToken.String)
            {
                try
                {
                    object obj = enumConverter.ReadJson(reader, objectType, existingValue, serializer);
                    if (obj is Enum)
                    {
                        list.Add((Enum)obj);
                    }
                }
                catch (JsonSerializationException ex)
                {
                    protocol.Log(LogCategory.Warning, "Enum value '{0}' could not be parsed. {1}".FormatInvariantWithArgs(reader.Value, ex.Message));
                }
                reader.Read();
            }
            return CombineEnumFlags(objectType.GetUnderlyingType(), list);
        }

        private static Enum CombineEnumFlags(Type enumType, IEnumerable<Enum> flags)
        {
            uint num = 0u;
            foreach (Enum flag in flags)
            {
                num |= (uint)(object)flag;
            }
            return Enum.ToObject(enumType, num) as Enum;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value != null)
            {
                Type underlyingType = value.GetType().GetUnderlyingType();
                Enum @enum = (Enum)value;
                writer.WriteStartArray();
                object obj = Enum.ToObject(underlyingType, 0);
                if (!obj.Equals(@enum))
                {
                    foreach (Enum value2 in Enum.GetValues(underlyingType))
                    {
                        if (!obj.Equals(value2) && @enum.HasFlag(value2))
                        {
                            enumConverter.WriteJson(writer, value2, serializer);
                        }
                    }
                }
                writer.WriteEndArray();
            }
        }
    }
}
