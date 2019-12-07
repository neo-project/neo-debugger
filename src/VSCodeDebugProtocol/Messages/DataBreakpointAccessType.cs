using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Serialization;
using System.Runtime.Serialization;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
    public enum DataBreakpointAccessType
    {
        [EnumMember(Value = "read")]
        Read = 0,
        [EnumMember(Value = "write")]
        Write = 1,
        [EnumMember(Value = "readWrite")]
        ReadWrite = 2,
        [DefaultEnumValue]
        Unknown = int.MaxValue
    }
}
