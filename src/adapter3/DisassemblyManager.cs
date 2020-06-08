using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Numerics;
using System.Text;
using Neo;
using Neo.SmartContract;
using Neo.VM;

namespace NeoDebug.Neo3
{
    class DisassemblyManager
    {
        private static readonly ImmutableDictionary<uint, string> sysCallNames;

        static DisassemblyManager()
        {
            sysCallNames = InteropService.SupportedMethods()
                .ToImmutableDictionary(d => d.Hash, d => d.Method);
        }

        private readonly Dictionary<int, ImmutableDictionary<int, int>> sourceMaps = new Dictionary<int, ImmutableDictionary<int, int>>();
        private readonly Dictionary<int, string> sources = new Dictionary<int, string>();

        public int GetLine(Neo.VM.Script script, int ip)
        {
            var hash = script.GetHashCode();
            if (!sourceMaps.TryGetValue(hash, out var map))
            {
                var digitCount = Utility.DigitCount(EnumerateInstructions(script).Last().ip);
                var padString = new string('0', digitCount);

                int line = 1;
                var sourceBuilder = new System.Text.StringBuilder();
                var sourceMapBuilder = ImmutableDictionary.CreateBuilder<int, int>();
                foreach (var t in EnumerateInstructions(script))
                {
                    sourceBuilder.Append($"{t.ip.ToString(padString)} {t.instruction.OpCode}");
                    if (!t.instruction.Operand.IsEmpty)
                    {
                        sourceBuilder.Append($" {GetOperandString(t.instruction)}");
                    }
                    var comment = GetComment(t.instruction, t.ip);
                    if (comment.Length > 0)
                    {
                        sourceBuilder.Append($" # {comment}");
                    }
                    sourceBuilder.Append("\n");
                    sourceMapBuilder.Add(t.ip, line++);
                }

                map = sourceMapBuilder.ToImmutable();
                sourceMaps[hash] = map;
                sources[hash] = sourceBuilder.ToString();
            }


            return map[ip];
        }

        public string GetSource(int scriptHash)
        {
            return sources[scriptHash];
        }

        static IEnumerable<(int ip, Instruction instruction)> EnumerateInstructions(Neo.VM.Script script)
        {
            int ip = 0;
            bool lastInstructionRet = false;
            while (ip < script.Length)
            {
                var instruction = script.GetInstruction(ip);
                lastInstructionRet = instruction.OpCode == OpCode.RET;
                yield return (ip, instruction);
                ip = ip + instruction.Size;
            }

            if (!lastInstructionRet)
            {
                yield return (ip, Instruction.RET);
            }
        }

        static string GetOperandString(Instruction instruction)
        {
            return string.Create<ReadOnlyMemory<byte>>(instruction.Operand.Length * 3 - 1,
                instruction.Operand, (span, memory) =>
                {
                    var first = memory.Span[0];
                    span[0] = GetHexValue(first / 16);
                    span[1] = GetHexValue(first % 16);

                    var index = 1;
                    for (var i = 2; i < span.Length; i += 3)
                    {
                        var b = memory.Span[index++];
                        span[i] = '-';
                        span[i + 1] = GetHexValue(b / 16);
                        span[i + 2] = GetHexValue(b % 16);
                    }
                });

            static char GetHexValue(int i) => (i < 10) ? (char)(i + '0') : (char)(i - 10 + 'A');
        }

        string GetComment(Instruction instruction, int ip)
        {
            switch (instruction.OpCode)
            {
                case OpCode.PUSHINT8:
                case OpCode.PUSHINT16:
                case OpCode.PUSHINT32:
                case OpCode.PUSHINT64:
                case OpCode.PUSHINT128:
                case OpCode.PUSHINT256:
                    return $"{new BigInteger(instruction.Operand.Span)}";
                case OpCode.PUSHM1:
                    return $"{(int)instruction.OpCode - (int)OpCode.PUSH0}";
                case OpCode.PUSHDATA1:
                case OpCode.PUSHDATA2:
                case OpCode.PUSHDATA4:
                    {
                        if (instruction.Operand.Length == 20)
                        {
                            return $"as script hash: {new UInt160(instruction.Operand.Span)}, as text: {Encoding.UTF8.GetString(instruction.Operand.Span)}";
                        }
                        return $"as text: {Encoding.UTF8.GetString(instruction.Operand.Span)}";
                    }
                case OpCode.SYSCALL:
                    return $"{sysCallNames[instruction.TokenU32]}";
                case OpCode.INITSLOT:
                    return $"{instruction.TokenU8} local variables, {instruction.TokenU8_1} arguments";
                case OpCode.JMP_L:
                case OpCode.JMPEQ_L:
                case OpCode.JMPGE_L:
                case OpCode.JMPGT_L:
                case OpCode.JMPIF_L:
                case OpCode.JMPIFNOT_L:
                case OpCode.JMPLE_L:
                case OpCode.JMPLT_L:
                case OpCode.JMPNE_L:
                case OpCode.CALL_L:
                    return OffsetComment(instruction.TokenI32);
                case OpCode.JMP:
                case OpCode.JMPEQ:
                case OpCode.JMPGE:
                case OpCode.JMPGT:
                case OpCode.JMPIF:
                case OpCode.JMPIFNOT:
                case OpCode.JMPLE:
                case OpCode.JMPLT:
                case OpCode.JMPNE:
                case OpCode.CALL:
                    return OffsetComment(instruction.TokenI8);
                default:
                    return string.Empty;
            }

            string OffsetComment(int offset) => $"pos: {ip + offset}, offset: {offset}";
        }
    }
}
