using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Protocol;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Utilities;
using Newtonsoft.Json;
using System;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Serialization
{
    internal abstract class EnumValueBase<T> : IJsonSerializable where T : struct, Enum
    {
        private string rawValue;

        public abstract bool ShouldSerialize
        {
            get;
        }

        public object Deserialize(JsonReader reader, JsonSerializer serializer, DebugProtocol protocol)
        {
            rawValue = (string)reader.Value;
            object value;
            if (string.IsNullOrEmpty(rawValue))
            {
                object defaultEnumValue = EnumUtilities.GetDefaultEnumValue<T>();
                SetValue((defaultEnumValue != null) ? ((T)defaultEnumValue) : default(T), isNull: true);
                rawValue = string.Empty;
            }
            else if (EnumUtilities.TryGetEnumValue<T>(rawValue, out value))
            {
                SetValue((T)value, isNull: false);
            }
            else
            {
                object defaultEnumValue2 = EnumUtilities.GetDefaultEnumValue<T>();
                if (defaultEnumValue2 == null)
                {
                    throw new JsonSerializationException("Value '{0}' is not valid for field of type '{1}', and no default enum value is available.".FormatInvariantWithArgs(rawValue, typeof(T).GetUnderlyingType().Name));
                }
                protocol.Log(LogCategory.Warning, "Value '{0}' is not valid for field of type '{1}'.".FormatInvariantWithArgs(rawValue, typeof(T).GetUnderlyingType().Name));
                SetValue((T)defaultEnumValue2, isNull: false);
            }
            return this;
        }

        public void Serialize(JsonWriter writer, JsonSerializer serializer, DebugProtocol protocol)
        {
            if (rawValue != null)
            {
                writer.WriteValue(rawValue);
                return;
            }
            if (!TryGetValue(out T value))
            {
                writer.WriteNull();
                return;
            }
            object defaultEnumValue = EnumUtilities.GetDefaultEnumValue<T>();
            if (defaultEnumValue != null && defaultEnumValue.Equals(value))
            {
                throw new JsonSerializationException("Default value for enum of type '{0}' is not allowed.".FormatInvariantWithArgs(typeof(T).Name));
            }
            if (!EnumUtilities.TryGetEnumValueName<T>(value, out string name))
            {
                throw new JsonSerializationException("Value '{0}' is not valid for enum of type '{1}'.".FormatInvariantWithArgs(value.ToString(), typeof(T).Name));
            }
            writer.WriteValue(name);
        }

        protected void OnValueChanged()
        {
            rawValue = null;
        }

        protected abstract bool TryGetValue(out T value);

        protected abstract void SetValue(T value, bool isNull);
    }
}
