namespace Neo.DebugAdapter
{
    class ContractParameter
    {
        public ContractParameterType Type;
        public object Value;

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
                    return System.Numerics.BigInteger.Parse(value);
                case ContractParameterType.String:
                    return value;
                default:
                    throw new System.NotImplementedException();
            }
        }
    }
}
