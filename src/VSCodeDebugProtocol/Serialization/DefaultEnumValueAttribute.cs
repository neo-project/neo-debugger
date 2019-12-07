using System;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Serialization
{
    [AttributeUsage(AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
    public sealed class DefaultEnumValueAttribute : Attribute
    {
    }
}
