using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using NeoDebug.Models;
using NeoFx;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace NeoDebug
{
    class BreakpointManager
    {
        private readonly ImmutableArray<Contract> contracts;
        private readonly DisassemblyManager disassemblyManager;
        private readonly Dictionary<(string path, UInt160 scriptHash), ImmutableHashSet<int>> breakpoints = new Dictionary<(string, UInt160), ImmutableHashSet<int>>();
        
        public BreakpointManager(IEnumerable<Contract> contracts, DisassemblyManager disassemblyManager)
        {
            this.contracts = contracts.ToImmutableArray();
            this.disassemblyManager = disassemblyManager;
        }

        public IEnumerable<Breakpoint> SetBreakpoints(Source source, IReadOnlyList<SourceBreakpoint> sourceBreakpoints)
        {
            if (UInt160.TryParse(source.Name, out var scriptHash))
            {
                return SetDisassemblyBreakpoints(scriptHash, source, sourceBreakpoints);
            }

            return SetSourceBreakpoints(source, sourceBreakpoints);
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

            this.breakpoints[(string.Empty, scriptHash)] = breakpoints.ToImmutableHashSet();
        }

        IEnumerable<Breakpoint> SetSourceBreakpoints(Source source, IReadOnlyList<SourceBreakpoint> sourceBreakpoints)
        {
            for (int i = 0; i < contracts.Length; i++)
            {
                var breakpoints = new HashSet<int>();
                var contract = contracts[i];
                var sequencePoints = contract.DebugInfo.Methods.SelectMany(m => m.SequencePoints)
                    .Where(sp => sp.Document.Equals(source.Path, StringComparison.InvariantCultureIgnoreCase))
                    .ToArray();

                for (int j = 0; j < sourceBreakpoints.Count; j++)
                {
                    var sourceBreakPoint = sourceBreakpoints[j];
                    var sequencePoint = Array.Find(sequencePoints, sp => sp.Start.line == sourceBreakPoint.Line);

                    if (sequencePoint != null)
                    {
                        breakpoints.Add(sequencePoint.Address);

                        yield return new Breakpoint()
                        {
                            Verified = true,
                            Column = sequencePoint.Start.column,
                            EndColumn = sequencePoint.End.column,
                            Line = sequencePoint.Start.line,
                            EndLine = sequencePoint.End.line,
                            Source = source

                        };
                    }
                    else
                    {
                        yield return new Breakpoint()
                        {
                            Verified = false,
                            Column = sourceBreakPoint.Column,
                            Line = sourceBreakPoint.Line,
                            Source = source
                        };
                    }
                }

                this.breakpoints[(source.Path, contract.ScriptHash)] = breakpoints.ToImmutableHashSet();
            }
        }

        public bool CheckBreakpoint(UInt160 scriptHash, int instructionPointer)
        {
            foreach (var kvp in breakpoints)
            {
                if (kvp.Key.scriptHash.Equals(scriptHash))
                {
                    return kvp.Value.Contains(instructionPointer);
                }
            }

            return false;
        }
    }
}
