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

            public int GetLine(Neo.VM.Script script, int ip)
            {
                var hash = script.GetHashCode();
                if (!sourceMaps.TryGetValue(hash, out var map))
                {
                    int line = 1;
                    var sourceBuilder = new System.Text.StringBuilder();
                    var sourceMapBuilder = ImmutableDictionary.CreateBuilder<int, int>();
                    foreach (var t in EnumerateInstructions(script))
                    {
                        sourceBuilder.Append($"{line} {t.ip} {t.instruction.OpCode}");
                        var comment = GetComment(t.instruction);
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
