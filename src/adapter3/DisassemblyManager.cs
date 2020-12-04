using System;
using System.Collections.Concurrent;
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
        public delegate bool TryGetDebugInfo(UInt160 scriptHash, [MaybeNullWhen(false)] out DebugInfo debugInfo);

        private static readonly ImmutableDictionary<uint, string> sysCallNames;

        static DisassemblyManager()
        {
            sysCallNames = ApplicationEngine.Services
                .ToImmutableDictionary(kvp => kvp.Value.Hash, kvp => kvp.Value.Name);
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

        private readonly ConcurrentDictionary<int, Disassembly> disassemblies = new ConcurrentDictionary<int, Disassembly>();
        private readonly TryGetScript tryGetScript;
        private readonly TryGetDebugInfo tryGetDebugInfo;

        public DisassemblyManager(TryGetScript tryGetScript, TryGetDebugInfo tryGetDebugInfo)
        {
            this.tryGetScript = tryGetScript;
            this.tryGetDebugInfo = tryGetDebugInfo;
        }

        public Disassembly GetDisassembly(IExecutionContext context, DebugInfo? debugInfo)
            => GetDisassembly(context.ScriptHash, context.Script, debugInfo);

        public Disassembly GetDisassembly(UInt160 scriptHash, Script script, DebugInfo? debugInfo)
            => disassemblies.GetOrAdd(scriptHash.GetHashCode(), sourceRef => ToDisassembly(sourceRef, scriptHash, script, debugInfo));

        public bool TryGetDisassembly(UInt160 scriptHash, out Disassembly disassembly)
        {
            if (tryGetScript(scriptHash, out var script))
            {
                var debugInfo = tryGetDebugInfo(scriptHash, out var _debugInfo) ? _debugInfo : null;
                disassembly = GetDisassembly(scriptHash, script, debugInfo);
                return true;
            }

            disassembly = default;
            return false;
        }

        public bool TryGetDisassembly(int sourceRef, out Disassembly disassembly)
            => disassemblies.TryGetValue(sourceRef, out disassembly);

        static Disassembly ToDisassembly(int sourceRef, UInt160 scriptHash, Script script, DebugInfo? debugInfo)
        {
            var digitCount = Utility.DigitCount(EnumerateInstructions(script).Last().address);
            var padString = new string('0', digitCount);

            var sourceBuilder = new StringBuilder();
            var addressMapBuilder = ImmutableDictionary.CreateBuilder<int, int>();
            var lineMapBuilder = ImmutableDictionary.CreateBuilder<int, int>();

            if (debugInfo == null)
            {
                var line = 1;
                foreach (var t in EnumerateInstructions(script))
                {
                    AddSource(sourceBuilder, t.address, t.instruction, padString);
                    addressMapBuilder.Add(t.address, line);
                    lineMapBuilder.Add(line, t.address);
                    line++;
                }
            }
            else
            {
                var instructions = EnumerateInstructions(script).ToList();
                int line = 1;
                foreach (var m in debugInfo.Methods.OrderBy(m => m.Range.Start))
                {
                    if (sourceBuilder.Length > 0) sourceBuilder.Append("\n");
                    sourceBuilder.Append($"# Start Method {m.Namespace}.{m.Name}");
                    line++;

                    var methodInstructions = instructions
                        .SkipWhile(t => t.address < m.Range.Start)
                        .TakeWhile(t => t.address <= m.Range.End);
                    foreach (var t in methodInstructions)
                    {
                        AddSource(sourceBuilder, t.address, t.instruction, padString);
                        addressMapBuilder.Add(t.address, line);
                        lineMapBuilder.Add(line, t.address);
                        line++;
                    }
                    sourceBuilder.Append($"\n# End Method {m.Namespace}.{m.Name}");
                    line++;
                }
            }

            return new Disassembly(
                scriptHash.ToString(),
                sourceBuilder.ToString(),
                sourceRef,
                addressMapBuilder.ToImmutable(),
                lineMapBuilder.ToImmutable());

            static void AddSource(StringBuilder sourceBuilder, int address, Instruction instruction, string padString)
            {
                if (sourceBuilder.Length > 0) sourceBuilder.Append("\n");

                sourceBuilder.Append($"{address.ToString(padString)} {instruction.OpCode}");
                if (!instruction.Operand.IsEmpty)
                {
                    sourceBuilder.Append($" {GetOperandString(instruction)}");
                }
                var comment = GetComment(instruction, address);
                if (comment.Length > 0)
                {
                    sourceBuilder.Append($" # {comment}");
                }
            }
        }

        static IEnumerable<(int address, Instruction instruction)> EnumerateInstructions(Script script)
        {
            var address = 0;
            var opcode = OpCode.PUSH0;
            while (address < script.Length)
            {
                var instruction = script.GetInstruction(address);
                opcode = instruction.OpCode;
                yield return (address, instruction);
                address += instruction.Size;
            }

            if (opcode != OpCode.RET)
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
                        var text = Encoding.UTF8.GetString(instruction.Operand.Span)
                            .Replace("\r", "\"\\r\"").Replace("\n", "\"\\n\"");
                        if (instruction.Operand.Length == 20)
                        {
                            return $"as script hash: {new UInt160(instruction.Operand.Span)}, as text: \"{text}\"";
                        }
                        return $"as text: \"{text}\"";
                    }
                case OpCode.SYSCALL:
                    return sysCallNames.TryGetValue(instruction.TokenU32, out var name)
                        ? name
                        : $"Unknown SysCall {instruction.TokenU32}";
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
