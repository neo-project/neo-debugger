using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Utilities
{
    internal static class ProtocolUtilities
    {
        private static readonly Regex VARIABLE = new Regex("\\{(\\w+)\\}");

        public static string ExpandVariables(string format, IDictionary<string, object> variables, bool underscoredOnly = true)
        {
            if (variables == null)
            {
                return format;
            }
            return VARIABLE.Replace(format, delegate (Match match)
            {
                string value = match.Groups[1].Value;
                object value2;
                return (!underscoredOnly || value.StartsWith("_", StringComparison.Ordinal)) ? ((!variables.TryGetValue(value, out value2)) ? "{{{0}: not found}}".FormatInvariantWithArgs(value) : value2.ToString()) : match.Groups[0].Value;
            });
        }
    }
}
