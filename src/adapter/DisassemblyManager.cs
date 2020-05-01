using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using NeoDebug.Models;
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

        string Comment(Models.Instruction i)
        {
            if (i.OpCode >= OpCode.PUSHBYTES1 && i.OpCode <= OpCode.PUSHBYTES75)
            {
                try
                {
                    var str = Encoding.UTF8.GetString(i.Operand.Span);
                    return $" # {str}";
                }
                catch
                {
                    // if decoding failed, just skip writing out the comment
                }
            }

            if (i.OpCode >= OpCode.JMP && i.OpCode <= OpCode.JMPIFNOT)
            {
                var offset = i.Position + BitConverter.ToUInt16(i.Operand.Span);
                return $" # {offset}";
            }

            if (i.OpCode == OpCode.SYSCALL)
            {
                var methodName = string.Empty;
                if (i.Operand.Length == 4)
                {
                    var methodHash = BitConverter.ToUInt32(i.Operand.Span);
                    methodName = methodNameResolver(methodHash);
                }
                else
                {
                    methodName = Encoding.UTF8.GetString(i.Operand.Span);
                }
                
                if (methodName.Length > 0)
                {
                    return $" # {methodName}";
                }
            }

            if (i.OpCode == OpCode.APPCALL || i.OpCode == OpCode.TAILCALL)
            {
                var scriptHash = new NeoFx.UInt160(i.Operand.Span);
                return $" # {scriptHash}";
            }

            return string.Empty;
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
                        var offset = instr.Position + BitConverter.ToUInt16(instr.Operand.Span.Slice(2));

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

        public void Add(byte[] script)
        {
            var hashCode = Crypto.Hash160(script).GetSequenceHashCode();

            var ipMap = new Dictionary<int, int>();
            var sb = new StringBuilder();
            var lineNumber = 1;
            var digitCount = DigitCount(Instruction.ParseScript(script).Last().Position);

            foreach (var instr in Instruction.ParseScript(script))
            {
                ipMap.Add(instr.Position, lineNumber++);

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
                sb.Append("\n");
            }
            
            sources.Add(hashCode, sb.ToString());
            sourceMap.Add(hashCode, ipMap.ToImmutableDictionary());
        }

        public StackFrame GetStackFrame(Neo.VM.ExecutionContext context, int index)
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
