using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using NeoDebug.Models;
using NeoFx;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using OpCode = Neo.VM.OpCode;

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

        string GetComment(in Instruction instr)
        {
            switch (instr.OpCode)
            {
                case OpCode opCode when opCode >= OpCode.PUSHBYTES1 && opCode <= OpCode.PUSHBYTES75:
                    {
                        return Encoding.UTF8.GetString(instr.Operand.Span);
                    }
                case OpCode.JMP:
                case OpCode.JMPIF:
                case OpCode.JMPIFNOT:
                case OpCode.CALL:
                    {
                        var offset = instr.Position + BitConverter.ToUInt16(instr.Operand.Span);
                        return $"position: {offset}";
                    }
                case OpCode.TAILCALL:
                case OpCode.APPCALL:
                   {
                        var scriptHash = new NeoFx.UInt160(instr.Operand.Span);
                        return scriptHash.ToString();
                   }
                case OpCode.CALL_I:
                    {
                        var returnCount = instr.Operand.Span[0];
                        var paramCount = instr.Operand.Span[1];
                        var offset = instr.Position + BitConverter.ToUInt16(instr.Operand.Span.Slice(2)) + 2;

                        return $"position: {offset}, param count: {paramCount}, return count: {returnCount}";
                    }

                case OpCode.CALL_E:
                case OpCode.CALL_ET:
                    {
                        var returnCount = instr.Operand.Span[0];
                        var paramCount = instr.Operand.Span[1];
                        var scriptHash = new NeoFx.UInt160(instr.Operand.Span.Slice(2));
                        return $"hash: {scriptHash}, param count: {paramCount}, return count: {returnCount}";
                    }
                case OpCode.CALL_ED:
                case OpCode.CALL_EDT:
                    {
                        var returnCount = instr.Operand.Span[0];
                        var paramCount = instr.Operand.Span[1];
                        return $"param count: {paramCount}, return count: {returnCount}";
                    }
                case OpCode.SYSCALL:
                    if (instr.Operand.Length == 4)
                    {
                        var methodHash = BitConverter.ToUInt32(instr.Operand.Span);
                        return methodNameResolver(methodHash);
                    }
                    else
                    {
                        return Encoding.UTF8.GetString(instr.Operand.Span);
                    }
                default:
                    return string.Empty;
            };
        }

        static int DigitCount(int n)
        {
            if (n < 10) return 1;
            if (n < 100) return 2;
            if (n < 1000) return 3;
            if (n < 10000) return 4;
            if (n < 100000) return 5;
            if (n < 1000000) return 6;
            if (n < 10000000) return 7;
            if (n < 100000000) return 8;
            if (n < 1000000000) return 9;
            return 10;
        }

        void WriteInstruction(in Instruction instr, StringBuilder sb, int digitCount)
        {
            if (sb.Length > 0) sb.Append("\n");

            var pos = instr.Position.ToString().PadLeft(digitCount, '0');

            var opcode = instr.OpCode >= OpCode.PUSHBYTES1 && instr.OpCode <= OpCode.PUSHBYTES75
                ? $"PUSHBYTES{(byte)instr.OpCode}" : instr.OpCode.ToString();
            sb.Append($"{pos} {opcode}");

            var operand = instr.Operand.IsEmpty
                ? string.Empty : BitConverter.ToString(instr.Operand.ToArray());
            if (operand.Length > 0)
            {
                sb.Append($" {operand}");
            }

            var comment = GetComment(instr);
            if (comment.Length > 0)
            {
                sb.Append($" # {comment}");
            }
        }

        public void Add(byte[] script, DebugInfo? debugInfo = null)
        {
            var digitCount = DigitCount(Instruction.ParseScript(script).Last().Position);
            var sb = new StringBuilder();
            var ipMap = new Dictionary<int, int>();

            if (debugInfo == null)
            {
                int line = 1;
                foreach (var instruction in Instruction.ParseScript(script))
                {
                    ipMap.Add(instruction.Position, line++);
                    WriteInstruction(instruction, sb, digitCount);
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
                        WriteInstruction(instruction, sb, digitCount);
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
            var hashCode = (new UInt160(scriptHash)).GetHashCode();
            var source = new Source()
            {
                SourceReference = hashCode,
                Name = scriptHash.ToHexString(),
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
