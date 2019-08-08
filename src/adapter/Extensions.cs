using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo.VM;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;

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

        public static bool CheckSequencePoint(this Contract contract, ExecutionContext context)
        {
            if (contract.ScriptHash.AsSpan().SequenceEqual(context.ScriptHash))
            {
                return (contract.GetMethod(context)?.SequencePoints ?? new List<SequencePoint>())
                    .Any(sp => sp.Address == context.InstructionPointer);
            }
            return false;
        }

        public static SequencePoint GetCurrentSequencePoint(this Method method, Neo.VM.ExecutionContext context)
        {
            return method?.SequencePoints.SingleOrDefault(sp => sp.Address == context.InstructionPointer);
        }
        
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

        public static BigInteger ParseBigInteger(this string value)
        {
            if (TryParseBigInteger(value, out var bigInteger))
            {
                return bigInteger;
            }

            throw new Exception($"could not parse {value} as BigInteger");
        }

        public static Variable GetVariable(this StackItem item, NeoDebugSession session, Parameter parameter = null)
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
                        var container = new ArrayContainer(session, array);
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

        public static bool TryGetValue(this StackItem item, string type, out string value)
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
    }
}
