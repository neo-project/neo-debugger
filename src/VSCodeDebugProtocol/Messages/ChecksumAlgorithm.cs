using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Serialization;
using System.Runtime.Serialization;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
	public enum ChecksumAlgorithm
	{
		[EnumMember(Value = "MD5")]
		MD5 = 0,
		[EnumMember(Value = "SHA1")]
		SHA1 = 1,
		[EnumMember(Value = "SHA256")]
		SHA256 = 2,
		[EnumMember(Value = "timestamp")]
		Timestamp = 3,
		[DefaultEnumValue]
		Unknown = int.MaxValue
	}
}
