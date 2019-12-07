using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Utilities
{
    public static class JsonUtilities
    {
        public static bool? FalseToNull(this bool value)
        {
            if (!value)
            {
                return null;
            }
            return true;
        }

        public static bool? FalseToNull(this bool? value)
        {
            if (value != true)
            {
                return null;
            }
            return true;
        }

        public static bool? TrueToNull(this bool value)
        {
            if (!value)
            {
                return false;
            }
            return null;
        }

        public static bool? TrueToNull(this bool? value)
        {
            if (value != false)
            {
                return null;
            }
            return false;
        }

        public static string EmptyToNull(this string value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                return value;
            }
            return null;
        }

        public static List<T> EmptyToNullList<T>(this IEnumerable<T> value)
        {
            if (value == null || !value.Any())
            {
                return null;
            }
            return value.ToList();
        }

        public static int? ZeroToNull(this int value)
        {
            if (value != 0)
            {
                return value;
            }
            return null;
        }

        public static string GetValueAsString(this IDictionary<string, JToken> properties, string propertyName)
        {
            return properties.GetValue<string>(propertyName, JTokenType.String);
        }

        public static bool? GetValueAsBool(this IDictionary<string, JToken> properties, string propertyName)
        {
            return properties.GetValue<bool?>(propertyName, JTokenType.Boolean);
        }

        public static int? GetValueAsInt(this IDictionary<string, JToken> properties, string propertyName)
        {
            return properties.GetValue<int?>(propertyName, JTokenType.Integer);
        }

        public static ulong? StringAsULong(string jsonString)
        {
            if (string.IsNullOrEmpty(jsonString))
            {
                return null;
            }
            NumberStyles style = NumberStyles.Number;
            if (jsonString.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                jsonString = jsonString.Substring(2);
                style = NumberStyles.HexNumber;
            }
            if (!ulong.TryParse(jsonString, style, CultureInfo.InvariantCulture, out ulong result))
            {
                return null;
            }
            return result;
        }

        public static Guid? GetValueAsGuid(this IDictionary<string, JToken> properties, string propertyName)
        {
            if (!Guid.TryParse(properties.GetValueAsString(propertyName), out Guid result))
            {
                return null;
            }
            return result;
        }

        public static T GetValue<T>(this IDictionary<string, JToken> properties, string propertyName, JTokenType type)
        {
            if (!properties.ContainsKey(propertyName))
            {
                return default(T);
            }
            JValue jValue = properties[propertyName] as JValue;
            if (jValue == null || jValue.Type != type)
            {
                return default(T);
            }
            return jValue.Value<T>();
        }

        public static JObject GetValueAsObject(this IDictionary<string, JToken> properties, string propertyName)
        {
            if (!properties.ContainsKey(propertyName))
            {
                return null;
            }
            return properties[propertyName] as JObject;
        }
    }
}
