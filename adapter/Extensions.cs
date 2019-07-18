using Neo.VM;
using System;
using System.Linq;

namespace Neo.DebugAdapter
{
    static class Extensions
    {
        public static string GetResult(this StackItem item)
        {
            switch (item)
            {
                case Neo.VM.Types.Boolean _:
                    return item.GetBoolean().ToString();
                case Neo.VM.Types.Integer _:
                    return item.GetBigInteger().ToString();
                case Neo.VM.Types.ByteArray _:
                    return BitConverter.ToString(item.GetByteArray());
                default:
                    throw new NotImplementedException();
            }
        }

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



        //public static Method GetCurrentMethod(this DebugInfo info, )
    }
}
