using NeoDebug.Models;
using NeoFx;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace NeoDebug
{
    class BreakpointManager
    {
        private readonly Dictionary<string, ImmutableHashSet<int>> sourceBreakpoints = new Dictionary<string, ImmutableHashSet<int>>();
        private ImmutableDictionary<UInt160, ImmutableHashSet<int>>? scriptBreakpoints = null;

        public void SetBreakpoints(string source, HashSet<int> breakpoints)
        {
            sourceBreakpoints[source] = breakpoints.ToImmutableHashSet();
            scriptBreakpoints = null;
        }

        public IReadOnlyDictionary<UInt160, ImmutableHashSet<int>> GetBreakpoints(Contract contract)
        {
            UInt160 GetScriptHash(KeyValuePair<string, ImmutableHashSet<int>> kvp)
                => UInt160.TryParse(kvp.Key, out var scriptHash)
                    ? scriptHash : contract.ScriptHash;

            if (scriptBreakpoints == null)
            {
                var local = sourceBreakpoints.GroupBy(GetScriptHash, kvp => kvp.Value)
                    .Select(g => (g.Key, g.Aggregate((h1, h2) => h1.Union(h2))))
                    .ToImmutableDictionary(t => t.Key, t => t.Item2);
                System.Threading.Interlocked.CompareExchange(ref scriptBreakpoints, local, null);
                return local;
            }

            return scriptBreakpoints;
        }
    }
}
