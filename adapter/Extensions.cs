using Neo.VM;
using System;
using System.Linq;

namespace Neo.DebugAdapter
{
    static class Extensions
    {
        public static Method GetEntryPoint(this Contract contract) => contract.DebugInfo.Methods.Single(m => m.Name == contract.DebugInfo.Entrypoint);

        public static Method GetMethod(this Contract contract, ExecutionContext context)
        {
            if (contract.ScriptHash.AsSpan().SequenceEqual(context.ScriptHash))
            {
                var ip = context.InstructionPointer;
                return contract.DebugInfo.Methods
                    .SingleOrDefault(m => m.StartAddress <= ip && ip <= m.EndAddress);
            }

            return null;
        }

        public static SequencePoint GetCurrentSequencePoint(this Method method, Neo.VM.ExecutionContext context)
        {
            return method.SequencePoints.SingleOrDefault(sp => sp.Address == context.InstructionPointer);
        }

        public static int GetParamCount(this Method method) => method.Parameters.Count + 1; // TODO: only add one if return value is not void

        public static string GetStackItemValue(this StackItem item, string type)
        {
            // TODO: don't use .NET typesnames
            switch (type)
            {
                case "System.Numerics.BigInteger":
                    return item.GetBigInteger().ToString();
                case "System.String":
                    return item.GetString();
                case "System.Object":
                    return GetStackItemValue(item);
                case "System.Object[]":
                    {
                        if (!(item is Neo.VM.Types.Array))
                            throw new ArgumentException();

                        return GetStackItemValue(item);
                    }
                default:
                    throw new NotImplementedException($"GetStackItemValue {type}");
            }
        }

        public static string GetStackItemValue(this StackItem item)
        {
            switch (item)
            {
                case Neo.VM.Types.Boolean _:
                    return item.GetBoolean().ToString();
                case Neo.VM.Types.Integer _:
                    return item.GetBigInteger().ToString();
                case Neo.VM.Types.ByteArray _:
                    return "<TBD byte array>";
                case Neo.VM.Types.Array array:
                    {
                        var builder = new System.Text.StringBuilder();
                        var first = true;
                        builder.Append("[");
                        for (int i = 0; i < array.Count; i++)
                        {
                            if (first)
                            {
                                first = false;
                            }
                            else
                            {
                                builder.Append(", ");
                            }

                            builder.Append(GetStackItemValue(array[i]));
                        }
                        builder.Append("]");
                        return builder.ToString();
                    }
                default:
                    throw new NotImplementedException($"GetStackItemValue {item.GetType().FullName}");
            }
        }
    }
}
