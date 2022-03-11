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
        // private readonly Dictionary<UInt160, ImmutableHashSet<int>> breakpointCache = new Dictionary<UInt160, ImmutableHashSet<int>>();
        // private readonly Dictionary<string, IReadOnlyList<SourceBreakpoint>> sourceBreakpointMap = new Dictionary<string, IReadOnlyList<SourceBreakpoint>>();

        public BreakpointManager(DisassemblyManager disassemblyManager, IReadOnlyList<DebugInfo> debugInfoList)
        {
            this.disassemblyManager = disassemblyManager;
            this.debugInfoList = debugInfoList;
        }

        readonly Dictionary<string, IReadOnlySet<(UInt160 hash, int position)>> sourceBreakpoints = new();
        readonly Dictionary<UInt160, IReadOnlySet<int>> breakpointCache = new();

        public IEnumerable<Breakpoint> SetBreakpoints(Source source, IReadOnlyList<SourceBreakpoint> sourceBreakpoints)
        {
            if (source.SourceReference.HasValue)
            {
                // TODO: disassembly breakpoint support
                // if (disassemblyManager.TryGet(source.SourceReference.Value, out var disassembly))
                // {
                    foreach (var sbp in sourceBreakpoints)
                    {
                        var validated = false; // disassembly.LineMap.TryGetValue(sbp.Line, out var address);

                        // store breakpoint address info

                        yield return new Breakpoint(validated)
                        {
                            Column = sbp.Column,
                            Line = sbp.Line,
                            Source = source
                        };
                    }
                // }
                // else
                // {
                //     // disassembly not yet generated for source.SourceReference
                // }
            }
            else
            {
                var sbpValidated = new bool[sourceBreakpoints.Count];
                HashSet<(UInt160, int)> breakpoints = new();

                for (int i = 0; i < debugInfoList.Count; i++)
                {
                    var debugInfo = debugInfoList[i];

                    if (!TryFindDocumentIndex(debugInfo.Documents, source.Path, out var index)) continue;

                    // TODO: Cache this?
                    var pointLookup = debugInfo.Methods
                        .SelectMany(m => m.SequencePoints)
                        .Where(sp => sp.Document == index)
                        .ToLookup(sp => sp.Start.line);

                    for (int j = 0; j < sourceBreakpoints.Count; j++)
                    {
                        SourceBreakpoint? sbp = sourceBreakpoints[j];
                        var validated = pointLookup.TryLookup(sbp.Line, out var points);

                        if (validated)
                        {
                            sbpValidated[j] = true;
                            breakpoints.Add((debugInfo.ScriptHash, points.First().Address));
                        }
                    }
                }

                this.sourceBreakpoints[source.Path] = breakpoints;

                for (int i = 0; i < sourceBreakpoints.Count; i++)
                {
                    var sbp = sourceBreakpoints[i];
                    yield return new Breakpoint(sbpValidated[i])
                    {
                        Column = sbp.Column,
                        Line = sbp.Line,
                        Source = source
                    };
                }
            }

            this.breakpointCache.Clear();

            var sob = this.sourceBreakpoints
                .SelectMany(kvp => kvp.Value)
                .GroupBy(t => t.hash);

            foreach (var g in sob)
            {
                this.breakpointCache[g.Key] = g.Select(t => t.position).Distinct().ToHashSet();
            }

            static bool TryFindDocumentIndex(IReadOnlyList<string> documents, string path, out int index)
            {
                for (int i = 0; i < documents.Count; i++)
                {
                    if (documents[i].Equals(path, StringComparison.InvariantCultureIgnoreCase))
                    {
                        index = i;
                        return true;
                    }
                }

                index = 0;
                return false;
            }
        }

        public bool CheckBreakpoint(IExecutionContext? context)
        {
            if (context is null) return false;

            if (breakpointCache.TryGetValue(context.ScriptIdentifier, out var set)
                && set.Contains(context.InstructionPointer))
            {
                return true;
            }

            return false;
        }

    }
}
