using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
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

        static string GetOpCodeString(OpCode opcode)
        {
            if (opcode >= OpCode.PUSHBYTES1 && opcode <= OpCode.PUSHBYTES75)
            {
                return $"PUSHBYTES{(byte)opcode}";
            }

            return $"{opcode}";
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

        string ZZZ(in Models.Instruction instr)
        {
            var opCodeStr = instr.OpCode >= OpCode.PUSHBYTES1 && instr.OpCode <= OpCode.PUSHBYTES75
                ? $"PUSHBYTES{(byte)instr.OpCode}" : instr.OpCode.ToString();

            return $"{instr.OpCode:x} {opCodeStr}\n";

            //    instr.OpCode switch
            //{
            //    OpCode opCode when 
            //        => $"PUSHBYTES{(byte)opCode}",
            //    _ => instr.OpCode.ToString()
            //};

            ////i.OpCode >= OpCode.PUSHBYTES1 && i.OpCode <= OpCode.PUSHBYTES75
            //switch (instr.OpCode)
            //{
            //    case OpCode opCode when opCode >= OpCode.PUSHBYTES1 && opCode <= OpCode.PUSHBYTES75:
            //        return $"PUSHBYTES{(byte)opCode} ";
            //    case OpCode opCode when opCode >= OpCode.JMP && opCode <= OpCode.JMPIFNOT:
            //        return $"{opCode}";
            //    case OpCode.APPCALL:
            //    case OpCode.TAILCALL:
            //        {
            //            var scriptHash = new NeoFx.UInt160(instr.Operand.Span);
            //            return $"{instr.OpCode} {scriptHash}";
            //        }
            //    case OpCode.SYSCALL:
            //        {
            //            var methodName = string.Empty;
            //            if (instr.Operand.Length == 4)
            //            {
            //                var methodHash = BitConverter.ToUInt32(instr.Operand.Span);
            //                methodName = methodNameResolver(methodHash);
            //            }
            //            else
            //            {
            //                methodName = Encoding.UTF8.GetString(instr.Operand.Span);
            //            }

            //            return $"{instr.OpCode} # {methodName}";
            //        }
            //    default:
                    return $"{instr.OpCode}";
            //}
        }

        public void Add(byte[] script)
        {
            var hashCode = Crypto.Hash160(script).GetSequenceHashCode();

            var ipMap = new Dictionary<int, int>();
            var sb = new StringBuilder();
            var lineNumber = 1;
            foreach (var i in Models.Instruction.ParseScript(script))
            {
                var operandString = i.Operand.IsEmpty
                    ? string.Empty
                    : BitConverter.ToString(i.Operand.ToArray());

                var commentString = Comment(i);

                ipMap.Add(i.Position, lineNumber++);
                //sb.Append($"{i.OpCode:x} {GetOpCodeString(i.OpCode)} {operandString} {commentString}\n");
                sb.Append(ZZZ(i));
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
