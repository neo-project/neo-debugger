using System;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Protocol
{
	[Flags]
	public enum DebugProtocolOptions
	{
		None = 0x0,
		IgnoreUnexpectedResponses = 0x1,
		AllowWildcardRegistrations = 0x2,
		ResponseCommandCaseInsensitive = 0x4,
		AllowResponseCommandNull = 0x8
	}
}
