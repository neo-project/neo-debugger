using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Numerics;
using Neo.SmartContract;
using Neo.VM;

namespace NeoDebug.Neo3
{
    partial class DebugSession
    {
        class DisassemblyManager
        {
            private readonly Dictionary<int, ImmutableDictionary<int, int>> sourceMaps = new Dictionary<int, ImmutableDictionary<int, int>>();
            private readonly Dictionary<int, string> sources = new Dictionary<int, string>();
            private readonly ImmutableDictionary<uint, string> sysCallNames; 

            public DisassemblyManager()
            {
                sysCallNames = InteropService.SupportedMethods().ToImmutableDictionary(d => d.Hash, d => d.Method);
            }

            static IEnumerable<(int ip, Instruction instruction)> EnumerateInstructions(Neo.VM.Script script)
            {
                int ip = 0;
                while (ip < script.Length)
                {
                    var instruction = script.GetInstruction(ip);
                    yield return (ip, instruction);
                    ip = ip + instruction.Size;
                }
            }

            public int GetLine(Neo.VM.Script script, int ip)
            {
                var hash = script.GetHashCode();
                var stringBuilder = new System.Text.StringBuilder();
                if (!sourceMaps.TryGetValue(hash, out var map))
                {
                    int line = 1;
                    var builder = ImmutableDictionary.CreateBuilder<int, int>();
                    foreach (var t in EnumerateInstructions(script))
                    {
                        stringBuilder.Append($"{line} {t.ip} {t.instruction.OpCode}");
                        var comment = GetComment(t.instruction);
                        if (comment.Length > 0)
                        {
                            stringBuilder.Append($" # {comment}");
                        }
                        stringBuilder.Append("\n");
                        builder.Add(t.ip, line++);
                    }
                    map = builder.ToImmutable();
                    sourceMaps[hash] = map;
                    sources[hash] = stringBuilder.ToString();
                }

                return map[ip];
            }

            public string GetSource(int scriptHash)
            {
                return sources[scriptHash];
            }

            string GetComment(Instruction instruction)
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
                        return BitConverter.ToString(instruction.Operand.ToArray());
                    case OpCode.SYSCALL:
                        return $"{sysCallNames[instruction.TokenU32]}";
                    default:
                        return string.Empty;
                }
            }
        }
    }
}
