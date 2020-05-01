using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo.VM;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace NeoDebug
{
    class SourceManager
    {
        private readonly Dictionary<int, string> sources = new Dictionary<int, string>();
        private readonly Dictionary<int, ImmutableDictionary<int, int>> sourceMap = new Dictionary<int, ImmutableDictionary<int, int>>();

        public void Add(byte[] script)
        {
            var hashCode = Crypto.Hash160(script).GetSequenceHashCode();

            var ipMap = new Dictionary<int, int>();
            var sb = new StringBuilder();
            var lineNumber = 1;
            foreach (var i in Models.Instruction.ParseScript(script))
            {
                ipMap.Add(i.Position, lineNumber++);
                sb.Append($"{i.OpCode:x} {i.OpCode}\n");
            }
            
            sources.Add(hashCode, sb.ToString());
            sourceMap.Add(hashCode, ipMap.ToImmutableDictionary());
        }

        public StackFrame GetStackFrame(ExecutionContext context, int index)
        {
            var hashCode = context.ScriptHash.GetSequenceHashCode();

            return new StackFrame
            {
                Id = index,
                Name = $"frame {index}",
                ModuleId = hashCode,
                Source = new Source()
                {
                    SourceReference = hashCode,
                    Name = Helpers.ToHexString(context.ScriptHash),
                },
                Line = sourceMap[hashCode][context.InstructionPointer],
                Column = 0,
            };
        }

        public string GetSource(int sourceReference)
        {
            return sources[sourceReference];
        }
    }
}
