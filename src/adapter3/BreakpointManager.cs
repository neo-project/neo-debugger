using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo;

namespace NeoDebug.Neo3
{
    class BreakpointManager
    {
        public delegate bool TryResolveAddressFunc(Source source, SourceBreakpoint sourceBreakpoint, out int address);

        private readonly Dictionary<int, ImmutableHashSet<int>> scriptBreakpoints = new Dictionary<int, ImmutableHashSet<int>>();

        private readonly DisassemblyManager disassemblyManager;

        public BreakpointManager(DisassemblyManager disassemblyManager)
        {
            this.disassemblyManager = disassemblyManager;
        }

        public IEnumerable<Breakpoint> SetBreakpoints(Source source, IReadOnlyList<SourceBreakpoint> sourceBreakpoints)
        {
            var disassembly = GetDisassembly();
            var breakpoints = ImmutableHashSet.CreateBuilder<int>();
            foreach (var sbp in sourceBreakpoints)
            {
                var address = disassembly.LineMap.TryGetValue(sbp.Line, out var _address) ? _address : -1;

                if (address >= 0)
                {
                    breakpoints.Add(address);
                }

                yield return new Breakpoint()
                {
                    Verified = address >= 0,
                    Column = sbp.Column,
                    Line = sbp.Line,
                    Source = source
                };
            }

            if (disassembly.SourceReference != 0)
            {
                this.scriptBreakpoints[disassembly.SourceReference] = breakpoints.ToImmutable();
            }

            DisassemblyManager.Disassembly GetDisassembly()
            {
                if (source.SourceReference.HasValue
                    && disassemblyManager.TryGetDisassembly(source.SourceReference.Value, out var disassembly))
                {
                    return disassembly;
                }

                if (UInt160.TryParse(source.Name, out var scriptHash)
                    && disassemblyManager.TryGetDisassembly(scriptHash, out disassembly))
                {
                    return disassembly;
                }

                return default;
            }
        }

        public bool CheckBreakpoint(Neo.VM.Script script, int instructionPointer)
        {
            return scriptBreakpoints.TryGetValue(script.GetHashCode(), out var bps)
                && bps.Contains(instructionPointer);
        }
    }
}
