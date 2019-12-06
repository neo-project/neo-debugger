using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Protocol;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Utilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Serialization
{
	internal class StringEnumConverterWithDefault : StringEnumConverter
	{
		private DebugProtocol protocol;

		public StringEnumConverterWithDefault(DebugProtocol protocol)
		{
			this.protocol = protocol;
		}

		public override bool CanConvert(Type objectType)
		{
			return objectType.IsStandardEnum();
		}

		public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
		{
			try
			{
				return base.ReadJson(reader, objectType, existingValue, serializer);
			}
			catch (JsonSerializationException)
			{
				if (reader.TokenType == JsonToken.String)
				{
					object defaultEnumValue = EnumUtilities.GetDefaultEnumValue(objectType);
					if (defaultEnumValue != null)
					{
						protocol.Log(LogCategory.Warning, "Value '{0}' is not valid for field of type '{1}'.".FormatInvariantWithArgs(reader.Value, objectType.GetUnderlyingType().Name));
						return defaultEnumValue;
					}
				}
				throw;
			}
		}

		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
		{
			object defaultEnumValue = EnumUtilities.GetDefaultEnumValue(value.GetType());
			if (defaultEnumValue != null && defaultEnumValue.Equals(value))
			{
				throw new JsonSerializationException("Default value for enum of type '{0}' is not allowed.".FormatInvariantWithArgs(value.GetType().Name));
			}
			base.WriteJson(writer, value, serializer);
		}
	}
}
