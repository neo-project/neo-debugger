using Neo.VM;
using NeoFx.Models;
using System;
using System.Linq;
using System.Numerics;

namespace NeoDebug.Models
{
    public readonly struct ContractArgument
    {
        public readonly ContractParameterType Type;
        public readonly object Value;

        public ContractArgument(ContractParameterType type, object value)
        {
            Type = type;
            Value = value;
        }

        public void EmitPush(ScriptBuilder builder)
        {
            switch (Type)
            {
                case ContractParameterType.Boolean:
                    builder.EmitPush((bool)Value);
                    break;
                case ContractParameterType.ByteArray:
                case ContractParameterType.Integer:
                    builder.EmitPush((BigInteger)Value);
                    break;
                case ContractParameterType.String:
                    builder.EmitPush((string)Value);
                    break;
                case ContractParameterType.Array:
                    {
                        var array = (ContractArgument[])Value;
                        foreach (var arg in array.Reverse())
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
