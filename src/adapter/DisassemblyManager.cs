using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using NeoDebug.Models;
using NeoFx;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace NeoDebug
{
    class DisassemblyManager
    {
        private readonly Dictionary<int, string> sources = new Dictionary<int, string>();
        private readonly Dictionary<int, ImmutableDictionary<int, int>> sourceMap = new Dictionary<int, ImmutableDictionary<int, int>>();
        private readonly Func<uint, string> methodNameResolver;

        public DisassemblyManager(Func<uint, string> methodNameResolver)
        {
            this.methodNameResolver = methodNameResolver;
        }

        public void Add(byte[] script, DebugInfo? debugInfo = null)
        {
            var digitCount = Utility.DigitCount(Instruction.ParseScript(script).Last().Position);
            var sb = new StringBuilder();
            var ipMap = new Dictionary<int, int>();

            if (debugInfo == null)
            {
                int line = 1;
                foreach (var instruction in Instruction.ParseScript(script))
                {
                    ipMap.Add(instruction.Position, line++);
                    instruction.Write(sb, methodNameResolver, digitCount);
                }
            }
            else
            {
                var instructions = Instruction.ParseScript(script).ToList();
                int line = 1;
                foreach (var m in debugInfo.Methods.OrderBy(m => m.Range.Start))
                {
                    if (sb.Length > 0) sb.Append("\n");
                    sb.Append($"# Start Method {m.Namespace}.{m.Name}");
                    line++;

                    var methodInstructions = instructions
                        .SkipWhile(i => i.Position < m.Range.Start)
                        .TakeWhile(i => i.Position <= m.Range.End);
                    foreach (var instruction in methodInstructions)
                    {
                        ipMap.Add(instruction.Position, line++);
                        instruction.Write(sb, methodNameResolver, digitCount);
                    }
                    sb.Append($"\n# End Method {m.Namespace}.{m.Name}");
                    line++;
                }
            }

            var hashCode = Crypto.HashScript(script).GetHashCode();
            sources.Add(hashCode, sb.ToString());
            sourceMap.Add(hashCode, ipMap.ToImmutableDictionary());
        }

        public (Source source, int line) GetSource(byte[] scriptHash, int instructionPointer)
        {
            var _scriptHash = new UInt160(scriptHash);
            var hashCode = _scriptHash.GetHashCode();
            var name = _scriptHash.ToString();
            var source = new Source()
            {
                SourceReference = hashCode,
                Name = name,
                Path = name,
                AdapterData = name,
            };

            return (source, sourceMap[hashCode][instructionPointer]);
        }

        public string GetSource(int sourceReference)
        {
            return sources[sourceReference];
        }

        public int GetInstructionPointer(in UInt160 scriptHash, int line)
        {
            if (sourceMap.TryGetValue(scriptHash.GetHashCode(), out var map))
            {
                foreach (var kvp in map)
                {
                    if (kvp.Value == line)
                    {
                        return kvp.Key;
                    }
                }
            }

            return -1;
        }
    }
}
