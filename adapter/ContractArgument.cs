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
                    throw new NotImplementedException();
            }
        }
    }
}
