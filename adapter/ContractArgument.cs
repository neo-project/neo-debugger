using Neo.VM;
using System;
using System.Numerics;

namespace Neo.DebugAdapter
{
    class ContractArgument
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
                default:
                    throw new System.NotImplementedException();
            }
        }

        private static ContractParameterType ParseContractParameterType(string type)
        {
            switch (type)
            {
                case "System.Numerics.BigInteger":
                    return ContractParameterType.Integer;
                default:
                    throw new NotImplementedException();
            }
        }

        public static ContractArgument FromArgument(string type, string value)
        {
            return FromArgument(ParseContractParameterType(type), value);
        }

        public static ContractArgument FromArgument(ContractParameterType type, string value)
        {
            return new ContractArgument()
            {
                Type = type,
                Value = Parse(type, value)
            };
        }

        public static object Parse(ContractParameterType type, string value)
        {
            switch (type)
            {
                case ContractParameterType.Boolean:
                    return bool.Parse(value);
                case ContractParameterType.Integer:
                    return BigInteger.Parse(value);
                case ContractParameterType.String:
                    return value;
                default:
                    throw new System.NotImplementedException();
            }
        }
    }
}
