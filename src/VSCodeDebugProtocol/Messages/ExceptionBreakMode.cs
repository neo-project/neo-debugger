using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Serialization;
using System.Runtime.Serialization;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
    public enum ExceptionBreakMode
    {
        [EnumMember(Value = "never")]
        Never = 0,
        [EnumMember(Value = "always")]
        Always = 1,
        [EnumMember(Value = "unhandled")]
        Unhandled = 2,
        [EnumMember(Value = "userUnhandled")]
        UserUnhandled = 3,
        [DefaultEnumValue]
        Unknown = int.MaxValue
    }
}
