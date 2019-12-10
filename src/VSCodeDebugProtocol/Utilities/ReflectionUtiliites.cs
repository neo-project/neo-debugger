using System;
using System.Reflection;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Utilities
{
    internal static class ReflectionUtiliites
    {
        public static bool HasAttribute<T>(this Type type) where T : Attribute
        {
            return (((object)type != null) ? type.GetCustomAttribute<T>() : null) != null;
        }

        public static bool IsNullable(this Type type)
        {
            if (type.IsGenericType)
            {
                return type.GetGenericTypeDefinition() == typeof(Nullable<>);
            }
            return false;
        }

        public static Type GetUnderlyingType(this Type type)
        {
            if (!type.IsNullable())
            {
                return type;
            }
            return Nullable.GetUnderlyingType(type);
        }

        public static bool IsStandardEnum(this Type type)
        {
            type = type.GetUnderlyingType();
            if (type.IsEnum)
            {
                return !type.HasAttribute<FlagsAttribute>();
            }
            return false;
        }

        public static bool IsFlagsEnum(this Type type)
        {
            type = type.GetUnderlyingType();
            if (type.IsEnum)
            {
                return type.HasAttribute<FlagsAttribute>();
            }
            return false;
        }
    }
}
