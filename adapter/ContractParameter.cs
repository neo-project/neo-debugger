using Neo.VM;
using System.Numerics;

namespace Neo.DebugAdapter
{
    class ContractParameter
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

        public static ContractParameter FromArgument(ContractParameterType type, string value)
        {
            return new ContractParameter()
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
