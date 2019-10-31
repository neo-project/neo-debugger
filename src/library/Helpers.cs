using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo.VM;
using NeoDebug.Models;
using NeoDebug.VariableContainers;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace NeoDebug
{
    public static class Helpers
    {
        //https://stackoverflow.com/a/1646913
        public static int GetSequenceHashCode(this ReadOnlySpan<byte> span)
        {
            unchecked
            {
                int hash = 17;
                for (int i = 0; i < span.Length; i++)
                {
                    hash = hash * 31 + span[i];
                }
                return hash;
            }
        }

        public static string ToHexString(this BigInteger bigInteger)
            => "0x" + bigInteger.ToString("x");

        public static string ToHexString(this ReadOnlySpan<byte> span)
            => ToHexString(new BigInteger(span));

        public static bool TryParseBigInteger(this string value, out BigInteger bigInteger)
        {
            if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                && BigInteger.TryParse(value.AsSpan().Slice(2), NumberStyles.HexNumber, null, out bigInteger))
            {
                return true;
            }

            Lazy<SHA256> sha256 = new Lazy<SHA256>(() => SHA256.Create());

            if (value.StartsWith("@", StringComparison.Ordinal))
            {
                Span<byte> tempBuffer = stackalloc byte[32];
                Span<byte> checksum = stackalloc byte[32];
                var decoded = SimpleBase.Base58.Bitcoin.Decode(value.AsSpan().Slice(1));

                if (decoded.Length == 25 // address version byte + 20 bytes address + 4 byte checksum
                    && decoded[0] == 23  // Address version 23 used by mainnet, testnet and Neo Express
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

        public static Event? GetEvent(this Contract contract, string name)
        {
            for (int i = 0; i < contract.DebugInfo.Events.Count; i++)
            {
                var @event = contract.DebugInfo.Events[i];
                if (@event.DisplayName == name)
                {
                    return @event;
                }
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

        private static readonly Regex indexRegex = new Regex(@"\[(\d+)\]$");

        public static readonly IReadOnlyDictionary<string, string> CastOperations = new Dictionary<string, string>()
            {
                { "int", "Integer" },
                { "bool", "Boolean" },
                { "string", "String" },
                { "hex", "HexString" },
                { "byte[]", "ByteArray" },
            }.ToImmutableDictionary();

        public static (string? typeHint, int? index, string name) ParseEvalExpression(string expression)
        {
            static (string? typeHint, string text) ParsePrefix(string input)
            {
                foreach (var kvp in CastOperations)
                {
                    if (input.Length > kvp.Key.Length + 2
                        && input[0] == '('
                        && input.AsSpan().Slice(1, kvp.Key.Length).SequenceEqual(kvp.Key)
                        && input[kvp.Key.Length + 1] == ')')
                    {
                        return (kvp.Value, input.Substring(kvp.Key.Length + 2));
                    }
                }

                return (null, input);
            }

            static (int? index, string text) ParseSuffix(string input)
            {
                var match = indexRegex.Match(input);
                if (match.Success)
                {
                    var matchValue = match.Groups[0].Value;
                    var indexValue = match.Groups[1].Value;
                    if (int.TryParse(indexValue, out var index))
                    {
                        return (index, input.Substring(0, input.Length - matchValue.Length));
                    }
                }
                return (null, input);
            }

            var prefix = ParsePrefix(expression);
            var suffix = ParseSuffix(prefix.text);

            return (prefix.typeHint, suffix.index, suffix.text.Trim());
        }

        internal static Variable GetVariable(this StackItem item, IVariableContainerSession session, string name, string? typeHint = null)
        {
            switch (typeHint)
            {
                case "Boolean":
                    return new Variable()
                    {
                        Name = name,
                        Value = item.GetBoolean().ToString(),
                        Type = "#Boolean",
                    };
                case "Integer":
                    return new Variable()
                    {
                        Name = name,
                        Value = item.GetBigInteger().ToString(),
                        Type = "#Integer",
                    };
                case "String":
                    return new Variable()
                    {
                        Name = name,
                        Value = item.GetString(),
                        Type = "#String",
                    };
                case "HexString":
                    return new Variable()
                    {
                        Name = name,
                        Value = item.GetBigInteger().ToHexString(),
                        Type = "#ByteArray"
                    };
                case "ByteArray":
                    return ByteArrayContainer.Create(session, item.GetByteArray(), name, true);
            }

            return item switch
            {
                IVariableProvider provider => provider.GetVariable(session, name),
                Neo.VM.Types.Boolean _ => new Variable()
                {
                    Name = name,
                    Value = item.GetBoolean().ToString(),
                    Type = "Boolean"
                },
                Neo.VM.Types.Integer _ => new Variable()
                {
                    Name = name,
                    Value = item.GetBigInteger().ToString(),
                    Type = "Integer"
                },
                Neo.VM.Types.ByteArray byteArray => ByteArrayContainer.Create(session, byteArray, name),
                Neo.VM.Types.InteropInterface _ => new Variable()
                {
                    Name = name,
                    Type = "InteropInterface"
                },
                Neo.VM.Types.Map map => NeoMapContainer.Create(session, map, name),
                // NeoArrayContainer.Create will detect Struct (which inherits from Array)
                // and distinguish accordingly
                Neo.VM.Types.Array array => NeoArrayContainer.Create(session, array, name),
                _ => throw new NotImplementedException($"GetStackItemValue {item.GetType().FullName}"),
            };
        }
    }
}
