using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Serialization;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Utilities
{
    internal static class EnumUtilities
    {
        private class EnumInfo
        {
            private Dictionary<string, object> nameToValueMap;

            private Dictionary<object, string> valueToNameMap;

            internal object DefaultValue
            {
                get;
            }

            internal EnumInfo(Type enumType)
            {
                nameToValueMap = new Dictionary<string, object>();
                valueToNameMap = new Dictionary<object, string>();
                if (!enumType.IsEnum)
                {
                    return;
                }
                FieldInfo[] fields = enumType.GetFields();
                foreach (FieldInfo fieldInfo in fields)
                {
                    if (!fieldInfo.IsSpecialName)
                    {
                        string text = fieldInfo.Name;
                        object obj;
                        try
                        {
                            obj = Enum.Parse(enumType, fieldInfo.Name);
                        }
                        catch (Exception)
                        {
                            continue;
                        }
                        EnumMemberAttribute customAttribute = fieldInfo.GetCustomAttribute<EnumMemberAttribute>();
                        if (customAttribute != null)
                        {
                            text = customAttribute.Value;
                        }
                        if (fieldInfo.GetCustomAttribute<DefaultEnumValueAttribute>() != null && DefaultValue == null)
                        {
                            DefaultValue = obj;
                        }
                        nameToValueMap.Add(text, obj);
                        valueToNameMap.Add(obj, text);
                    }
                }
            }

            internal bool TryGetValue(string rawValue, out object value)
            {
                return nameToValueMap.TryGetValue(rawValue, out value);
            }

            internal bool TryGetValueName(object value, out string name)
            {
                return valueToNameMap.TryGetValue(value, out name);
            }
        }

        private static Dictionary<Type, EnumInfo> enumInfo = new Dictionary<Type, EnumInfo>();

        private static object enumInfoLock = new object();

        internal static bool TryGetEnumValue<T>(string rawValue, out object value) where T : Enum
        {
            return GetEnumInfo(typeof(T)).TryGetValue(rawValue, out value);
        }

        internal static object GetDefaultEnumValue<T>() where T : Enum
        {
            return GetEnumInfo(typeof(T)).DefaultValue;
        }

        internal static object GetDefaultEnumValue(Type enumType)
        {
            return GetEnumInfo(enumType).DefaultValue;
        }

        internal static bool TryGetEnumValueName<T>(object value, out string name) where T : Enum
        {
            return GetEnumInfo(typeof(T)).TryGetValueName(value, out name);
        }

        internal static bool TryGetEnumValueName(Type enumType, object value, out string name)
        {
            return GetEnumInfo(enumType).TryGetValueName(value, out name);
        }

        private static EnumInfo GetEnumInfo(Type enumType)
        {
            enumType = enumType.GetUnderlyingType();
            lock (enumInfoLock)
            {
                if (enumInfo.TryGetValue(enumType, out EnumInfo value))
                {
                    return value;
                }
                value = new EnumInfo(enumType);
                enumInfo.Add(enumType, value);
                return value;
            }
        }
    }
}
