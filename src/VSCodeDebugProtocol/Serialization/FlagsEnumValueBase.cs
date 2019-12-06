using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Protocol;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Utilities;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Serialization
{
	internal abstract class FlagsEnumValueBase<T> : IJsonSerializable where T : struct, Enum
	{
		private string[] rawValues;

		public abstract bool ShouldSerialize
		{
			get;
		}

		public object Deserialize(JsonReader reader, JsonSerializer serializer, DebugProtocol protocol)
		{
			List<string> list = new List<string>();
			if (reader.TokenType == JsonToken.StartArray && reader.Read())
			{
				while (reader.TokenType == JsonToken.String)
				{
					list.Add((string)reader.Value);
					reader.Read();
				}
			}
			rawValues = list.ToArray();
			if (list.Any())
			{
				uint num = 0u;
				foreach (string item in list)
				{
					if (EnumUtilities.TryGetEnumValue<T>(item, out object value))
					{
						num |= (uint)value;
					}
					else
					{
						protocol.Log(LogCategory.Warning, "Value '{0}' is not valid for field of type '{1}'.".FormatInvariantWithArgs(item, typeof(T).GetUnderlyingType().Name));
					}
				}
				T value2 = (T)Enum.ToObject(typeof(T), num);
				SetValue(value2, isNull: false);
			}
			else
			{
				SetValue(default(T), isNull: true);
			}
			return this;
		}

		public void Serialize(JsonWriter writer, JsonSerializer serializer, DebugProtocol protocol)
		{
			IEnumerable<string> enumerable = null;
			T value;
			if (rawValues != null)
			{
				enumerable = rawValues;
			}
			else if (TryGetValue(out value))
			{
				List<string> list = new List<string>();
				object obj = Enum.ToObject(typeof(T), 0);
				if (!obj.Equals(value))
				{
					foreach (Enum value2 in Enum.GetValues(typeof(T)))
					{
						if (!obj.Equals(value2) && value.HasFlag(value2) && EnumUtilities.TryGetEnumValueName<T>(value2, out string name))
						{
							list.Add(name);
						}
					}
				}
				enumerable = list;
			}
			writer.WriteStartArray();
			if (enumerable != null)
			{
				foreach (string item in enumerable)
				{
					writer.WriteValue(item);
				}
			}
			writer.WriteEndArray();
		}

		protected void OnValueChanged()
		{
			rawValues = null;
		}

		protected abstract bool TryGetValue(out T value);

		protected abstract void SetValue(T value, bool isNull);
	}
}
