using Neo.VM;
using Newtonsoft.Json.Linq;
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

        public static ContractArgument FromArgument(string type, JToken value)
        {
            return FromArgument(ParseContractParameterType(type), value);
        }

        public static ContractArgument FromArgument(ContractParameterType type, JToken value)
        {
            return new ContractArgument()
            {
                Type = type,
                Value = Parse(type, value)
            };
        }

        public static object Parse(ContractParameterType type, JToken value)
        {
            switch (type)
            {
                case ContractParameterType.Boolean:
                    if (value.Type == JTokenType.Boolean)
                        return value.Value<bool>();
                    return bool.Parse(value.ToString());
                case ContractParameterType.Integer:
                    if (value.Type == JTokenType.Integer)
                        return new BigInteger(value.Value<int>());
                    return BigInteger.Parse(value.ToString());
                case ContractParameterType.String:
                    return value.ToString();
                default:
                    throw new NotImplementedException();
            }
        }
    }
}
