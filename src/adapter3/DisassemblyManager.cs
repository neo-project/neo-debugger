﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using System.Text;
using Neo;
using Neo.SmartContract;
using Neo.VM;
using Helper = Neo.SmartContract.Helper;

namespace NeoDebug.Neo3
{
    class DisassemblyManager
    {
        public delegate bool TryGetScript(UInt160 scriptHash, [MaybeNullWhen(false)] out Script script);

        private static readonly ImmutableDictionary<uint, string> sysCallNames;

        static DisassemblyManager()
        {
            sysCallNames = InteropService.SupportedMethods()
                .ToImmutableDictionary(d => d.Hash, d => d.Method);
        }

        public readonly struct Disassembly
        {
            public readonly string Name;
            public readonly string Source;
            public readonly int SourceReference;
            public readonly ImmutableDictionary<int, int> AddressMap;
            public readonly ImmutableDictionary<int, int> LineMap;

            public Disassembly(string name, string source, int sourceReference, ImmutableDictionary<int, int> addressMap, ImmutableDictionary<int, int> lineMap)
            {
                this.Name = name;
                this.Source = source;
                this.SourceReference = sourceReference;
                this.AddressMap = addressMap;
                this.LineMap = lineMap;
            }
        }

        private readonly TryGetScript tryGetScript;
        private readonly Dictionary<int, Disassembly> disassemblies = new Dictionary<int, Disassembly>();

        public DisassemblyManager(TryGetScript tryGetScript)
        {
            this.tryGetScript = tryGetScript;
        }

        public bool TryGetDisassembly(UInt160 scriptHash, out Disassembly disassembly)
        {
            if (tryGetScript(scriptHash, out var script))
            {
                disassembly = GetDisassembly(script);
                return true;
            }

            disassembly = default;
            return false;
        }

        public bool TryGetDisassembly(int sourceRef, out Disassembly disassembly) 
            => disassemblies.TryGetValue(sourceRef, out disassembly);

        public Disassembly GetDisassembly(Script script)
        {
            var sourceRef = script.GetHashCode();
            if (!disassemblies.TryGetValue(sourceRef, out var disassembly))
            {
                var digitCount = Utility.DigitCount(EnumerateInstructions(script).Last().address);
                var padString = new string('0', digitCount);

                var sourceBuilder = new StringBuilder();
                var addressMapBuilder = ImmutableDictionary.CreateBuilder<int, int>();
                var lineMapBuilder = ImmutableDictionary.CreateBuilder<int, int>();

                var line = 1;
                foreach (var t in EnumerateInstructions(script))
                {
                    sourceBuilder.Append($"{t.address.ToString(padString)} {t.instruction.OpCode}");
                    if (!t.instruction.Operand.IsEmpty)
                    {
                        sourceBuilder.Append($" {GetOperandString(t.instruction)}");
                    }
                    var comment = GetComment(t.instruction, t.address);
                    if (comment.Length > 0)
                    {
                        sourceBuilder.Append($" # {comment}");
                    }
                    sourceBuilder.Append("\n");
                    addressMapBuilder.Add(t.address, line);
                    lineMapBuilder.Add(line, t.address);
                    line++;
                }

                var name = Helper.ToScriptHash(script).ToString();

                disassembly = new Disassembly(
                    name,
                    sourceBuilder.ToString(),
                    sourceRef,
                    addressMapBuilder.ToImmutable(),
                    lineMapBuilder.ToImmutable());
                disassemblies[sourceRef] = disassembly;
            }

            return disassembly;
        }

        static IEnumerable<(int address, Instruction instruction)> EnumerateInstructions(Neo.VM.Script script)
        {
            int address = 0;
            bool lastInstructionRet = false;
            while (address < script.Length)
            {
                var instruction = script.GetInstruction(address);
                lastInstructionRet = instruction.OpCode == OpCode.RET;
                yield return (address, instruction);
                address = address + instruction.Size;
            }

            if (!lastInstructionRet)
            {
                yield return (address, Instruction.RET);
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

        static string GetComment(Instruction instruction, int ip)
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
