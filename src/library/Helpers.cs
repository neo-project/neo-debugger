using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo.VM;
using NeoDebug.Models;
using NeoDebug.VariableContainers;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;

namespace NeoDebug
{
    public static class Helpers
    {
        public static bool TryParseBigInteger(this string value, out BigInteger bigInteger)
        {
            if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                && BigInteger.TryParse(value.AsSpan().Slice(2), NumberStyles.HexNumber, null, out bigInteger))
            {
                return true;
            }

            Lazy<SHA256> sha256 = new Lazy<SHA256>(() => SHA256.Create());

            // All NEO addresses start with an 'A' and are 34 characters long
            if (value.StartsWith("@A", StringComparison.Ordinal))
            {
                Span<byte> tempBuffer = stackalloc byte[32];
                Span<byte> checksum = stackalloc byte[32];
                var decoded = SimpleBase.Base58.Bitcoin.Decode(value.AsSpan().Slice(1));

                if (decoded.Length == 25 // address version byte + 20 bytes address + 4 byte checksum
                    && decoded[0] == 23  // Address version 23 used by mainnet, testnet and NEO Express
                    && sha256.Value.TryComputeHash(decoded.Slice(0, 21), tempBuffer, out var written1)
                    && sha256.Value.TryComputeHash(tempBuffer, checksum, out var written2)
                    && written1 == 32 && written2 == 32
                    && decoded.Slice(21).SequenceEqual(checksum.Slice(0, 4)))
                {
                    bigInteger = new BigInteger(decoded.Slice(1, 20));
                    return true;
                }
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

            value = string.Empty;
            return false;
        }

        internal static Method? GetMethod(this Contract contract, ExecutionContext context)
        {
            if (contract.ScriptHash.AsSpan().SequenceEqual(context.ScriptHash))
            {
                var ip = context.InstructionPointer;
                return contract.DebugInfo.Methods
                    .SingleOrDefault(m => m.StartAddress <= ip && ip <= m.EndAddress);
            }

            return null;
        }

        internal static bool CheckSequencePoint(this Contract contract, ExecutionContext context)
        {
            if (contract.ScriptHash.AsSpan().SequenceEqual(context.ScriptHash))
            {
                return (contract.GetMethod(context)?.SequencePoints ?? new List<SequencePoint>())
                    .Any(sp => sp.Address == context.InstructionPointer);
            }
            return false;
        }

        internal static SequencePoint? GetCurrentSequencePoint(this Method method, ExecutionContext context)
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

        internal static Variable GetVariable2(this StackItem item, IVariableContainerSession session, string name)
        {
            switch (item)
            {
                case IVariableProvider provider:
                    return provider.GetVariable(session, name);
                case Neo.VM.Types.Boolean _:
                    return new Variable()
                    {
                        Name = name,
                        Value = item.GetBoolean().ToString(),
                        Type = "Boolean"
                    };
                case Neo.VM.Types.Integer _:
                    return new Variable()
                    {
                        Name = name,
                        Value = item.GetBigInteger().ToString(),
                        Type = "Integer"
                    };
                case Neo.VM.Types.ByteArray byteArray:
                    return ByteArrayContainer.Create(session, byteArray, name);
                case Neo.VM.Types.InteropInterface _:
                    return new Variable()
                    {
                        Name = name,
                        Type = "InteropInterface"
                    };
                case Neo.VM.Types.Map map:
                    return NeoMapContainer.Create(session, map, name);
                case Neo.VM.Types.Array array:
                    // Note, NeoArrayContainer.Create will sniff if array is actually
                    // a struct and adjust accordingly
                    return NeoArrayContainer.Create(session, array, name);
                default:
                    throw new NotImplementedException($"GetStackItemValue {item.GetType().FullName}");
            }
        }

        internal static Variable GetVariable(this StackItem item, IVariableContainerSession session, Parameter? parameter = null)
        {
            if (parameter?.Type == "ByteArray")
            {
                return ByteArrayContainer.Create(session, item.GetByteArray(), parameter?.Name);
            }

            if (parameter != null 
                && item.TryGetValue(parameter.Type, out var value))
            {
                return new Variable()
                {
                    Name = parameter.Name,
                    Value = value,
                    Type = parameter.Type,
                    EvaluateName = parameter.Name,
                };
            }

            switch (item)
            {
                case IVariableProvider provider:
                    return provider.GetVariable(session, "");
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
                    return ByteArrayContainer.Create(session, byteArray, parameter?.Name);
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
