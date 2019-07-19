using Neo.VM;
using System;
using System.Linq;

namespace Neo.DebugAdapter
{
    static class Extensions
    {
        public static Function GetAbiEntryPoint(this AbiInfo info) => info.Functions.Single(f => f.Name == info.Entrypoint);
        public static Method GetEntryMethod(this DebugInfo info) => info.Methods.Single(m => m.Name == info.Entrypoint);

        public static Method GetMethod(this Contract contract, ExecutionContext context)
        {
            if (contract.ScriptHash.AsSpan().SequenceEqual(context.ScriptHash))
            {
                var ip = context.InstructionPointer;
                return contract.DebugInfo.Methods.SingleOrDefault(m => m.StartAddress <= ip || m.EndAddress >= ip);
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
                default:
                    throw new NotImplementedException();
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
                default:
                    return item.GetType().FullName;
            }
        }
    }
}
