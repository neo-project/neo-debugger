using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo;
using Neo.BlockchainToolkit.Models;
using Neo.Cryptography.ECC;
using Neo.SmartContract;
using Newtonsoft.Json.Linq;

namespace NeoDebug.Neo3
{
    using StackItem = Neo.VM.Types.StackItem;
    using StackItemType = Neo.VM.Types.StackItemType;
    using ByteString = Neo.VM.Types.ByteString;

    internal static class Extensions
    {
        public static bool StartsWith<T>(this ReadOnlyMemory<T> @this, ReadOnlySpan<T> value)
            where T : IEquatable<T>
        {
            return @this.Length >= value.Length
                && @this.Slice(0, value.Length).Span.SequenceEqual(value);
        }

        public static DebugInfo.Method? GetMethod(this DebugInfo debugInfo, int instructionPointer)
        {
            return debugInfo.Methods
                .SingleOrDefault(m => m.Range.Start <= instructionPointer && instructionPointer <= m.Range.End);
        }

        public static bool TryGetMethod(this DebugInfo debugInfo, int instructionPointer, [MaybeNullWhen(false)] out DebugInfo.Method method)
        {
            method = debugInfo.GetMethod(instructionPointer);
            return method != null;
        }

        public static string? GetDocumentPath(this DebugInfo.SequencePoint? @this, DebugInfo? debugInfo)
        {
            if (@this != null && debugInfo != null
                && @this.Document >= 0
                && @this.Document < debugInfo.Documents.Count)
            {
                return debugInfo.Documents[@this.Document];
            }

            return null;
        }

        public static bool PathEquals(this DebugInfo.SequencePoint? @this, DebugInfo? debugInfo, string path)
        {
            return string.Equals(@this.GetDocumentPath(debugInfo), path, StringComparison.OrdinalIgnoreCase);
        }

        public static DebugInfo.SequencePoint GetCurrentSequencePoint(this DebugInfo.Method method, int instructionPointer)
        {
            var sequencePoints = method.SequencePoints;
            if (sequencePoints.Count == 0)
            {
                throw new InvalidOperationException($"{method.Name} has no sequence points");
            }

            for (int i = sequencePoints.Count - 1; i >= 0; i--)
            {
                if (instructionPointer >= sequencePoints[i].Address)
                    return sequencePoints[i];
            }

            return sequencePoints[0];
        }

        //https://stackoverflow.com/a/1646913
        public static int GetSequenceHashCode(this ReadOnlySpan<byte> span)
        {
            unchecked
            {
                int hash = 17;
                for (int i = 0; i < span.Length; i++)
                {
                    hash = (hash * 31) + span[i];
                }
                return hash;
            }
        }

        public static int GetSequenceHashCode(this byte[] array)
        {
            return GetSequenceHashCode(array.AsSpan());
        }

        public static JToken ToJson(this StackItem item)
        {
            return item switch
            {
                Neo.VM.Types.Boolean _ => item.GetBoolean(),
                Neo.VM.Types.Buffer buffer => buffer.GetSpan().ToHexString(),
                Neo.VM.Types.ByteString byteString => byteString.GetSpan().ToHexString(),
                Neo.VM.Types.Integer @int => @int.GetInteger().ToString(),
                // Neo.VM.Types.InteropInterface _ => MakeVariable("InteropInterface"),
                Neo.VM.Types.Map map => MapToJson(map),
                Neo.VM.Types.Null _ => new JValue((object?)null),
                // Neo.VM.Types.Pointer _ => MakeVariable("Pointer"),
                Neo.VM.Types.Array array => new JArray(array.Select(i => i.ToJson())),
                _ => throw new NotSupportedException(),
            };

            static JObject MapToJson(Neo.VM.Types.Map map)
            {
                var json = new JObject();
                foreach (var (key, value) in map)
                {
                    json.Add(PrimitiveTypeToString(key), value.ToJson());
                }
                return json;
            }

            static string PrimitiveTypeToString(Neo.VM.Types.PrimitiveType item)
            {
                try
                {
                    return item.GetString() ?? throw new Exception();
                }
                catch
                {
                    return Convert.ToHexString(item.GetSpan());
                }
            }
        }

        // public static string? TryConvert(this StackItem item, CastOperation typeHint = CastOperation.None)
        // {
        //     try
        //     {
        //         return typeHint switch
        //         {
        //             CastOperation.Boolean => item.GetBoolean().ToString(),
        //             CastOperation.Integer => item.IsNull
        //                 ? BigInteger.Zero.ToString()
        //                 : ((Neo.VM.Types.Integer)item.ConvertTo(StackItemType.Integer)).GetInteger().ToString(),
        //             CastOperation.String => item.GetString(),
        //             CastOperation.HexString => ToHexString(item),
        //             CastOperation.ByteArray => ToHexString(item),
        //             // CastOperation.Address => ToAddress(item, 0x35),
        //             _ => null
        //         };
        //     }
        //     catch
        //     {
        //         return null;
        //     }

        //     // static string? ToAddress(StackItem item, byte version = 0x35)
        //     // {
        //     //     var span = item.GetSpan();
        //     //     if (span.Length == UInt160.Length)
        //     //     {
        //     //         var uint160 = new UInt160(span);
        //     //         return Neo.Wallets.Helper.ToAddress(uint160, version);
        //     //     }

        //     //     return null;
        //     // }

        //     static string ToHexString(StackItem item) => item.IsNull
        //         ? "<null>"
        //         : ((ByteString)item.ConvertTo(StackItemType.ByteString)).GetSpan().ToHexString();
        // }

        // public static T TryConvert<T>(this StackItem item) where T : StackItem
        // {
        //     switch (typeof(T))
        //     {
        //         case typeof(Neo.VM.Types.Integer):
        //             break;

        //     }

        // }

        public static Variable ToVariable(this StackItem item, IVariableManager manager, string name, ContractParameterType parameterType)
        {
            try
            {
                Variable? variable = parameterType switch
                {
                    ContractParameterType.Boolean => NewVariable(item.GetBoolean()),
                    ContractParameterType.ByteArray => ConvertByteArray(),
                    ContractParameterType.Hash160 => NewVariable(new UInt160(item.GetSpan())),
                    ContractParameterType.Hash256 => NewVariable(new UInt256(item.GetSpan())),
                    ContractParameterType.Integer => NewVariable(item.GetInteger()),
                    ContractParameterType.PublicKey => NewVariable(ECPoint.DecodePoint(item.GetSpan(), ECCurve.Secp256r1)),
                    ContractParameterType.Signature => ConvertByteArray(),
                    ContractParameterType.String => NewVariable(item.GetString()),
                    _ => null
                };

                if (variable != null) return variable;
            }
            catch { }

            return item.ToVariable(manager, name);

            Variable? NewVariable(object? obj) => obj == null ? null : new Variable { Name = name, Value = obj.ToString(), Type = parameterType.ToString() };

            Variable? ConvertByteArray()
            {
                if (item.IsNull) return new Variable { Name = name, Value = "<null>", Type = parameterType.ToString() };
                if (item is Neo.VM.Types.Buffer buffer) return ByteArrayContainer.Create(manager, buffer, name);
                if (item is Neo.VM.Types.ByteString byteString) return ByteArrayContainer.Create(manager, byteString, name);
                if (item is Neo.VM.Types.PrimitiveType)
                {
                    byteString = (ByteString)item.ConvertTo(StackItemType.ByteString);
                    return ByteArrayContainer.Create(manager, (ReadOnlyMemory<byte>)byteString, name);
                }
                return null;
            }
        }

        public static Variable ToVariable(this StackItem item, IVariableManager manager, string name)
        {
            return item switch
            {
                Neo.VM.Types.Array array => NeoArrayContainer.Create(manager, array, name),
                Neo.VM.Types.Boolean _ => new Variable { Name = name, Value = $"{item.GetBoolean()}", Type = "Boolean" },
                Neo.VM.Types.Buffer buffer => ByteArrayContainer.Create(manager, buffer, name),
                Neo.VM.Types.ByteString byteString => ByteArrayContainer.Create(manager, byteString, name),
                Neo.VM.Types.Integer @int => new Variable { Name = name, Value = $"{@int.GetInteger()}", Type = "Boolean" },
                Neo.VM.Types.InteropInterface _ => new Variable { Name = name, Value = "InteropInterface" },
                Neo.VM.Types.Map map => NeoMapContainer.Create(manager, map, name),
                Neo.VM.Types.Null _ => new Variable { Name = name, Value = "<null>", Type = "Null" },
                Neo.VM.Types.Pointer _ => new Variable { Name = name, Value = "Pointer" },
                _ => throw new NotSupportedException(),
            };
        }
    }
}
