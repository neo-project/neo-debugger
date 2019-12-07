using System.Globalization;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Utilities
{
    internal static class StringExtensions
    {
        public static string FormatInvariantWithArgs(this string format, params object[] args)
        {
            return string.Format(CultureInfo.InvariantCulture, format, args);
        }
    }
}
