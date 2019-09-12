using Neo.VM;
using System;
using System.Linq;
using System.Numerics;

namespace NeoDebug.Models
{
    public class ContractArgument
    {
        public ContractParameterType Type;
        public object Value;

        public void EmitPush(ScriptBuilder builder)
        {
            switch (Type)
            {
                case ContractParameterType.Boolean:
                    builder.EmitPush((bool)Value);
                    break;
                case ContractParameterType.Integer:
                    builder.EmitPush((BigInteger)Value);
                    break;
                case ContractParameterType.String:
                    builder.EmitPush((string)Value);
                    break;
                case ContractParameterType.Array:
                    {
                        var array = (object[])Value;
                        foreach (var arg in array.Cast<ContractArgument>().Reverse())
                        {
                            arg.EmitPush(builder);
                        }
                        builder.EmitPush(array.Length);
                        builder.Emit(OpCode.PACK);

                    }
                    break;
                default:
                    throw new NotImplementedException($"EmitPush {Type}");
            }
        }
    }
}
