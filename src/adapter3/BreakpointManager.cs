using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo;
using Neo.BlockchainToolkit.Models;

namespace NeoDebug.Neo3
{
    class BreakpointManager
    {
        private readonly DisassemblyManager disassemblyManager;
        private readonly IReadOnlyList<DebugInfo> debugInfoList;
        private readonly Dictionary<UInt160, ImmutableHashSet<int>> breakpointCache = new Dictionary<UInt160, ImmutableHashSet<int>>();
        private readonly Dictionary<string, IReadOnlyList<SourceBreakpoint>> sourceBreakpointMap = new Dictionary<string, IReadOnlyList<SourceBreakpoint>>();

        public BreakpointManager(DisassemblyManager disassemblyManager, IReadOnlyList<DebugInfo> debugInfoList)
        {
            this.disassemblyManager = disassemblyManager;
            this.debugInfoList = debugInfoList;
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
                var sequencePoints = debugInfoList
                    .SelectMany(d => d.Methods.SelectMany(m => m.SequencePoints).Select(sp => (d, sp)))
                    .Where(t => t.sp.PathEquals(t.d, source.Path))
                    .Select(t => t.sp)
                    .ToImmutableList();

                foreach (var sbp in sourceBreakpoints)
                {
                    yield return new Breakpoint()
                    {
                        Verified = sequencePoints.Any(sp => sp.Start.Line == sbp.Line),
                        Column = sbp.Column,
                        Line = sbp.Line,
                        Source = source
                    };
                }
            }
        }

        private IReadOnlySet<int> GetBreakpoints(UInt160 scriptHash)
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
                            IReadOnlyList<DebugInfo.SequencePoint> sequencePoints = debugInfo.Methods
                                .SelectMany(m => m.SequencePoints)
                                .Where(sp => sp.PathEquals(debugInfo, kvp.Key))
                                .ToList();

                            foreach (var sbp in kvp.Value)
                            {
                                if (sequencePoints.TryFind(sp => sp.Start.Line == sbp.Line, out var found))
                                {
                                    builder.Add(found.Address);
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

        public bool CheckBreakpoint(UInt160 scriptHash, int? instructionPointer)
            => instructionPointer.HasValue
                && GetBreakpoints(scriptHash).Contains(instructionPointer.Value);
    }
}
