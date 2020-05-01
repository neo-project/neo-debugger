using Neo.VM;
using System;
using System.Collections.Generic;
using System.Text;

namespace NeoDebug.Models
{
    readonly struct Instruction
    {
        public readonly OpCode OpCode;
        public readonly ReadOnlyMemory<byte> Operand;
        public readonly int Position;

        public Instruction(OpCode opCode, ReadOnlyMemory<byte> operand, int position)
        {
            OpCode = opCode;
            Operand = operand;
            Position = position;
        }

        public static IEnumerable<Instruction> ParseScript(ReadOnlyMemory<byte> script)
        {
            int pos = 0;
            OpCode lastOpcode = OpCode.PUSH0;
            while (pos < script.Length)
            {
                var initialPos = pos;
                var opcode = (OpCode)script.Span[pos++];
                lastOpcode = opcode;
                var operandSizePrefix = opcode switch
                {
                    OpCode.PUSHDATA1 => 1,
                    OpCode.PUSHDATA2 => 2,
                    OpCode.PUSHDATA4 => 4,
                    OpCode.SYSCALL => 1,
                    _ => 0
                };

                int operandSize = operandSizePrefix switch
                {
                    1 => script.Span[pos],
                    2 => BitConverter.ToUInt16(script.Span.Slice(pos, 2)),
                    4 => BitConverter.ToUInt16(script.Span.Slice(pos, 4)),
                    _ => opcode switch
                    {
                        OpCode.JMP => 2,
                        OpCode.JMPIF => 2,
                        OpCode.JMPIFNOT => 2,
                        OpCode.CALL => 2,
                        OpCode.APPCALL => 20,
                        OpCode.TAILCALL => 20,
                        OpCode.CALL_I => 4,
                        OpCode.CALL_E => 22,
                        OpCode.CALL_ED => 2,
                        OpCode.CALL_ET => 22,
                        OpCode.CALL_EDT => 2,
                        _ when opcode >= OpCode.PUSHBYTES1 && opcode <= OpCode.PUSHBYTES75 => (int)opcode,
                        _ => 0
                    }
                };

                byte[] operand = Array.Empty<byte>();
                if (operandSize > 0)
                {
                    pos += operandSizePrefix;
                    if (pos + operandSize > script.Length)
                    {
                        throw new InvalidOperationException();
                    }

                    operand = script.Slice(pos, operandSize).ToArray();
                    pos += operandSize;
                }

                yield return new Instruction(opcode, operand.AsMemory(), initialPos);
            }

            if (lastOpcode != OpCode.RET)
            {
                yield return new Instruction(OpCode.RET, default, pos);
            }
        }
    }
}
