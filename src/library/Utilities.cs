using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo.VM;
using NeoDebug.Models;
using NeoDebug.VariableContainers;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text;

namespace NeoDebug
{
    public static class Utilities
    {
        public static bool TryParseBigInteger(this string value, out BigInteger bigInteger)
        {
            if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                && BigInteger.TryParse(value.AsSpan().Slice(2), NumberStyles.HexNumber, null, out bigInteger))
            {
                return true;
            }

            bigInteger = default;
            return false;
        }

        internal static bool TryGetValue(this StackItem item, string type, out string value)
        {
            if (item != null && !string.IsNullOrEmpty(type))
            {
                switch (type)
                {
                    case "Boolean":
                        value = item.GetBoolean().ToString();
                        return true;
                    case "Integer":
                        value = item.GetBigInteger().ToString();
                        return true;
                    case "String":
                        value = item.GetString();
                        return true;
                }
            }

            value = default;
            return false;
        }

        internal static bool Compare(byte[] a1, byte[] a2)
        {
            // Note, ScriptHash.AsSpan().SequenceEqual would be preferanble to Utilities.Compare, 
            //       but Span isn't available in netstandard2.0

            if (a1.Length != a2.Length)
                return false;

            for (int i = 0; i < a1.Length; i++)
            {
                if (a1[i] != a2[i])
                {
                    return false;
                }
            }

            return true;
        }

        internal static Method GetMethod(this Contract contract, ExecutionContext context)
        {
            if (Compare(contract.ScriptHash, context.ScriptHash))
            {
                var ip = context.InstructionPointer;
                return contract.DebugInfo.Methods
                    .SingleOrDefault(m => m.StartAddress <= ip && ip <= m.EndAddress);
            }

            return null;
        }

        internal static bool CheckSequencePoint(this Contract contract, ExecutionContext context)
        {
            if (Compare(contract.ScriptHash, context.ScriptHash))
            {
                return (contract.GetMethod(context)?.SequencePoints ?? new List<SequencePoint>())
                    .Any(sp => sp.Address == context.InstructionPointer);
            }
            return false;
        }

        internal static SequencePoint GetCurrentSequencePoint(this Method method, ExecutionContext context)
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
                        if (ip > sequencePoints[i].Address && ip <= sequencePoints[i+1].Address)
                            return sequencePoints[i];
                    }
                }
            }

            return null;
        }

        internal static Variable GetVariable(this StackItem item, IVariableContainerSession session, Parameter parameter = null)
        {
            if (parameter?.Type == "ByteArray")
            {
                return ByteArrayContainer.GetVariable(item.GetByteArray(), session, parameter?.Name);
            }

            if (item.TryGetValue(parameter?.Type, out var value))
            {
                return new Variable()
                {
                    Name = parameter.Name,
                    Value = value,
                    Type = parameter.Type
                };
            }

            switch (item)
            {
                case IVariableProvider provider:
                    return provider.GetVariable(session);
                case Neo.VM.Types.Boolean _:
                    return new Variable()
                    {
                        Name = parameter?.Name,
                        Value = item.GetBoolean().ToString(),
                        Type = "Boolean"
                    };
                case Neo.VM.Types.Integer _:
                    return new Variable()
                    {
                        Name = parameter?.Name,
                        Value = item.GetBigInteger().ToString(),
                        Type = "Integer"
                    };
                case Neo.VM.Types.InteropInterface _:
                    return new Variable()
                    {
                        Name = parameter?.Name,
                        Value = "<interop interface>"
                    };
                case Neo.VM.Types.Struct _: // struct before array
                case Neo.VM.Types.Map _:
                    return new Variable()
                    {
                        Name = parameter?.Name,
                        Value = item.GetType().Name
                    };
                case Neo.VM.Types.ByteArray byteArray:
                    return ByteArrayContainer.GetVariable(byteArray, session, parameter?.Name);
                case Neo.VM.Types.Array array:
                    {
                        var container = new NeoArrayContainer(session, array);
                        var containerID = session.AddVariableContainer(container);
                        return new Variable()
                        {
                            Name = parameter?.Name,
                            Type = $"Array[{array.Count}]",
                            VariablesReference = containerID,
                            IndexedVariables = array.Count,
                        };
                    }
                default:
                    throw new NotImplementedException($"GetStackItemValue {item.GetType().FullName}");
            }
        }
    }
}
