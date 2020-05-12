using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using NeoFx;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace NeoDebug
{
    class BreakpointManager
    {
        private readonly DisassemblyManager disassemblyManager;
        private readonly Dictionary<string, ImmutableHashSet<int>> sourceBreakpoints;

        public BreakpointManager()
        {
            sourceBreakpoints = new Dictionary<string, ImmutableHashSet<int>>();
        }

        public IEnumerable<Breakpoint> SetBreakpoints(Source source, IReadOnlyList<SourceBreakpoint> sourceBreakpoints)
        {
            if (UInt160.TryParse(source.Name, out var scriptHash))
            {
                return SetDisassemblyBreakpoints(scriptHash, source, sourceBreakpoints);
            }

            return sourceBreakpoints.Select(sbp => new Breakpoint
            {
                Verified = false,
                Column = sbp.Column,
                Line = sbp.Line,
                Source = source
            });
        }

        IEnumerable<Breakpoint> SetDisassemblyBreakpoints(UInt160 scriptHash, Source source, IReadOnlyList<SourceBreakpoint> sourceBreakpoints)
        {
            var breakpoints = new HashSet<int>();
            for (int i = 0; i < sourceBreakpoints.Count; i++)
            {
                var sourceBreakPoint = sourceBreakpoints[i];
                var ip = disassemblyManager.GetInstructionPointer(scriptHash, sourceBreakPoint.Line);

                if (ip >= 0)
                {
                    breakpoints.Add(ip);
                }

                yield return new Breakpoint()
                {
                    Verified = ip >= 0,
                    Column = sourceBreakPoint.Column,
                    Line = sourceBreakPoint.Line,
                    Source = source
                };
            }

            this.sourceBreakpoints[source.Name] = breakpoints.ToImmutableHashSet();
        }


        public bool CheckBreakpoint(UInt160 scriptHash, int instructionPointer)
        {
            // Dictionary<UInt160, ImmutableHashSet<int>> breakpointMap = null!;

            // if (breakpointMap.TryGetValue(scriptHash, out var breakpoints)
            //     && breakpoints.Contains(instructionPointer))
            // {
            //     return true;
            // }

            return false;
        }
    }
}
