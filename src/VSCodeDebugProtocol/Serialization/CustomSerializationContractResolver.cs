using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Reflection;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Serialization
{
    internal class CustomSerializationContractResolver : DefaultContractResolver
    {
        private static Lazy<CustomSerializationContractResolver> _instanceLazy = new Lazy<CustomSerializationContractResolver>(() => new CustomSerializationContractResolver());

        public static CustomSerializationContractResolver Instance => _instanceLazy.Value;

        private CustomSerializationContractResolver()
        {
        }

        protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
        {
            JsonProperty jsonProperty = base.CreateProperty(member, memberSerialization);
            if (typeof(IJsonSerializable).IsAssignableFrom(jsonProperty.PropertyType))
            {
                IValueProvider valueProvider = CreateMemberValueProvider(member);
                jsonProperty.ShouldSerialize = ((object instance) => (valueProvider.GetValue(instance) as IJsonSerializable)?.ShouldSerialize ?? true);
            }
            return jsonProperty;
        }
    }
}
