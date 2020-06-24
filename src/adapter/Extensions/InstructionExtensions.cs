using NeoDebug.Models;
using System;
using System.Text;
using OpCode = Neo.VM.OpCode;

namespace NeoDebug
{
    static class InstructionExtensions
    {
        public static void Write(this in Instruction instr, StringBuilder sb, Func<uint, string> methodNameResolver, int digitCount)
        {
            if (sb.Length > 0) sb.Append("\n");

            sb.Append($"{instr.AsPosition(digitCount)} {instr.AsOpCode()}");

            var operand = instr.AsOperand();
            if (operand.Length > 0)
            {
                sb.Append($" {operand}");
            }

            var comment = instr.AsComment(methodNameResolver);
            if (comment.Length > 0)
            {
                sb.Append($" # {comment}");
            }
        }

        public static string AsPosition(this in Instruction @this, int digitCount)
            => @this.Position.ToString().PadLeft(digitCount, '0');

        public static string AsOpCode(this in Instruction @this)
            => @this.OpCode >= OpCode.PUSHBYTES1 && @this.OpCode <= OpCode.PUSHBYTES75
                ? $"PUSHBYTES{(byte)@this.OpCode}" : @this.OpCode.ToString();

        public static string AsOperand(this in Instruction @this)
            => @this.Operand.IsEmpty
                ? string.Empty : BitConverter.ToString(@this.Operand.ToArray());

        public static string AsComment(this in Instruction @this, Func<uint, string> methodNameResolver)
        {
            switch (@this.OpCode)
            {
                case OpCode.PUSH0:
                    return "PUSH0 and PUSHF use the same opcode value";
                case OpCode.PUSH1:
                    return "PUSH1 and PUSHT use the same opcode value";
                case OpCode opCode when opCode >= OpCode.PUSHBYTES1 && opCode <= OpCode.PUSHBYTES75:
                    {
                        return Encoding.UTF8.GetString(@this.Operand.Span);
                    }
                case OpCode.JMP:
                case OpCode.JMPIF:
                case OpCode.JMPIFNOT:
                case OpCode.CALL:
                    {
                        var offset = @this.Position + BitConverter.ToUInt16(@this.Operand.Span);
                        return $"position: {offset}";
                    }
                case OpCode.TAILCALL:
                case OpCode.APPCALL:
                    {
                        var scriptHash = new NeoFx.UInt160(@this.Operand.Span);
                        return scriptHash.ToString();
                    }
                case OpCode.CALL_I:
                    {
                        var returnCount = @this.Operand.Span[0];
                        var paramCount = @this.Operand.Span[1];
                        var offset = @this.Position + BitConverter.ToUInt16(@this.Operand.Span.Slice(2)) + 2;

                        return $"position: {offset}, param count: {paramCount}, return count: {returnCount}";
                    }

                case OpCode.CALL_E:
                case OpCode.CALL_ET:
                    {
                        var returnCount = @this.Operand.Span[0];
                        var paramCount = @this.Operand.Span[1];
                        var scriptHash = new NeoFx.UInt160(@this.Operand.Span.Slice(2));
                        return $"hash: {scriptHash}, param count: {paramCount}, return count: {returnCount}";
                    }
                case OpCode.CALL_ED:
                case OpCode.CALL_EDT:
                    {
                        var returnCount = @this.Operand.Span[0];
                        var paramCount = @this.Operand.Span[1];
                        return $"param count: {paramCount}, return count: {returnCount}";
                    }
                case OpCode.SYSCALL:
                    if (@this.Operand.Length == 4)
                    {
                        var methodHash = BitConverter.ToUInt32(@this.Operand.Span);
                        return methodNameResolver(methodHash);
                    }
                    else
                    {
                        return Encoding.UTF8.GetString(@this.Operand.Span);
                    }
                default:
                    return string.Empty;
            };
        }
    }
}
