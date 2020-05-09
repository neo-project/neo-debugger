using Neo.VM;
using NeoDebug.Models;
using NeoFx;
using System.Collections.Generic;
using System.Linq;

namespace NeoDebug
{
    public static class ContractExtensions
    {
        public static DebugInfo.Method? GetMethod(this Contract contract, ExecutionContext context)
        {
            if (contract.ScriptHash == new UInt160(context.ScriptHash))
            {
                var ip = context.InstructionPointer;
                return contract.DebugInfo.Methods
                    .SingleOrDefault(m => m.Range.Start <= ip && ip <= m.Range.End);
            }

            return null;
        }

        public static DebugInfo.Event? GetEvent(this Contract contract, string name)
        {
            for (int i = 0; i < contract.DebugInfo.Events.Count; i++)
            {
                var @event = contract.DebugInfo.Events[i];
                if (@event.Name == name)
                {
                    return @event;
                }
            }

            return null;
        }

        public static bool CheckSequencePoint(this Contract contract, ExecutionContext context)
        {
            if (contract.ScriptHash == new UInt160(context.ScriptHash))
            {
                return (contract.GetMethod(context)?.SequencePoints ?? new List<DebugInfo.SequencePoint>())
                    .Any(sp => sp.Address == context.InstructionPointer);
            }
            return false;
        }

        public static DebugInfo.SequencePoint? GetCurrentSequencePoint(this DebugInfo.Method method, ExecutionContext context)
        {
            if (method != null)
            {
                var sequencePoints = method.SequencePoints.OrderBy(sp => sp.Address).ToArray();
                if (sequencePoints.Length > 0)
                {
                    var ip = context.InstructionPointer;

                    for (int i = 0; i < sequencePoints.Length; i++)
                    {
                        if (ip == sequencePoints[i].Address)
                            return sequencePoints[i];
                    }

                    if (ip <= sequencePoints[0].Address)
                        return sequencePoints[0];

                    for (int i = 0; i < sequencePoints.Length - 1; i++)
                    {
                        if (ip > sequencePoints[i].Address && ip <= sequencePoints[i + 1].Address)
                            return sequencePoints[i];
                    }
                }
            }

            return null;
        }
    }
}
