using System;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Utilities
{
	public static class TimestampUtilities
	{
		private const long TICKS_PER_SEC = 10000000L;

		private const long UNIX_EPOCH_OFFSET = 11644473600L;

		public static long ToUnixTimestamp(this DateTime timestamp)
		{
			return Math.Max(timestamp.ToFileTimeUtc() / 10000000 - 11644473600L, 0L);
		}

		public static DateTime UnixTimestampToDateTime(long unixTimestamp)
		{
			return DateTime.FromFileTimeUtc((unixTimestamp + 11644473600L) * 10000000);
		}
	}
}
