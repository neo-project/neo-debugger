using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo;

namespace NeoDebug.Neo3
{
    class BreakpointManager
    {
        private readonly DisassemblyManager disassemblyManager;
        private readonly ImmutableList<DebugInfo> debugInfoList;
        private readonly Dictionary<UInt160, ImmutableHashSet<int>> breakpointCache = new Dictionary<UInt160, ImmutableHashSet<int>>();
        private readonly Dictionary<string, IReadOnlyList<SourceBreakpoint>> sourceBreakpointMap = new Dictionary<string, IReadOnlyList<SourceBreakpoint>>();

        public BreakpointManager(DisassemblyManager disassemblyManager, IEnumerable<DebugInfo> debugInfo)
        {
            this.disassemblyManager = disassemblyManager;

            this.debugInfoList = debugInfo.ToImmutableList();
        }

        public IEnumerable<Breakpoint> SetBreakpoints(Source source, IReadOnlyList<SourceBreakpoint> sourceBreakpoints)
        {
            breakpointCache.Clear();
            this.sourceBreakpointMap[source.Path] = sourceBreakpoints;

            if (UInt160.TryParse(source.Name, out var scriptHash))
            {
                var lineMap = disassemblyManager.TryGetDisassembly(scriptHash, out var disassembly)
                    ? disassembly.LineMap : ImmutableDictionary<int, int>.Empty;

                foreach (var sbp in sourceBreakpoints)
                {
                    yield return new Breakpoint()
                    {
                        Verified = lineMap.TryGetValue(sbp.Line, out var _) ? true : false,
                        Column = sbp.Column,
                        Line = sbp.Line,
                        Source = source
                    };
                }
            }
            else
            {
                var sequencePoints = debugInfoList.SelectMany(d => d.Methods.SelectMany(m => m.SequencePoints))
                    .Where(sp => sp.Document.Equals(source.Path, StringComparison.InvariantCultureIgnoreCase))
                    .ToList();
                    
                foreach (var sbp in sourceBreakpoints)
                {
                    yield return new Breakpoint()
                    {
                        Verified = sequencePoints.Any(sp => sp.Start.line == sbp.Line),
                        Column = sbp.Column,
                        Line = sbp.Line,
                        Source = source
                    };
                }
            }
        }

        ImmutableHashSet<int> GetBreakpoints(UInt160 scriptHash)
        {
            if (!breakpointCache.TryGetValue(scriptHash, out var breakpoints))
            {
                var builder = ImmutableHashSet.CreateBuilder<int>();

                foreach (var kvp in sourceBreakpointMap)
                {
                    if (UInt160.TryParse(kvp.Key, out var sourceScriptHash))
                    {
                        if (sourceScriptHash == scriptHash)
                        {
                            var lineMap = disassemblyManager.TryGetDisassembly(scriptHash, out var disassembly)
                                ? disassembly.LineMap : ImmutableDictionary<int, int>.Empty;

                            foreach (var sbp in kvp.Value)
                            {
                                if (lineMap.TryGetValue(sbp.Line, out var address))
                                {
                                    builder.Add(address);
                                }
                            }
                        }
                    }
                    else
                    {
                        foreach (var debugInfo in debugInfoList)
                        {
                            var sequencePoints = debugInfo.Methods
                                .SelectMany(m => m.SequencePoints)
                                .Where(sp => sp.Document.Equals(kvp.Key, StringComparison.InvariantCultureIgnoreCase))
                                .ToList();

                            foreach (var sbp in kvp.Value)
                            {
                                var foundSP = sequencePoints.Find(sp => sp.Start.line == sbp.Line);

                                if (foundSP != null)
                                {
                                    builder.Add(foundSP.Address);
                                }
                            }
                        }
                    }
                }

                breakpoints = builder.ToImmutable();
                breakpointCache[scriptHash] = breakpoints;
            }

            return breakpoints;
        }

        public bool CheckBreakpoint(UInt160 scriptHash, int instructionPointer)
            => GetBreakpoints(scriptHash).Contains(instructionPointer);
    }
}
