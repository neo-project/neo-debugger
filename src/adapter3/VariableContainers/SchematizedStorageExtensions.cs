using System;
using System.Collections.Generic;
using System.Numerics;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo;
using Neo.BlockchainToolkit.Models;
using Neo.Cryptography.ECC;

using StackItem = Neo.VM.Types.StackItem;
using NeoArray = Neo.VM.Types.Array;
using NeoMap = Neo.VM.Types.Map;
using ContractParameterType = Neo.SmartContract.ContractParameterType;

namespace NeoDebug.Neo3
{
    readonly record struct KeySegment(string Name, PrimitiveType Type, object Value);

    static class SchematizedStorageExtensions
    {
        public static string AsString(this KeySegment @this, byte addressVersion)
            => @this.Type == PrimitiveType.Address && @this.Value is UInt160 uint160
                ? uint160.AsAddress(addressVersion) : $"{@this.Value}";

        public static string AsTypeName(this ContractType type)
            => type switch
            {
                PrimitiveContractType primitiveType => $"{primitiveType.Type}",
                StructContractType structType => structType.Name,
                MapContractType mapType => $"Map<{mapType.KeyType}, {mapType.ValueType.AsTypeName()}>",
                _ => throw new ArgumentException(type.GetType().Name, nameof(type)),
            };

        public static IEnumerable<KeySegment> AsKeySegments(this ReadOnlyMemory<byte> buffer, StorageDef storageDef)
        {
            buffer = buffer.Slice(storageDef.KeyPrefix.Length);

            for (int i = 0; i < storageDef.KeySegments.Count; i++)
            {
                var segment = storageDef.KeySegments[i];

                object value;
                switch (segment.Type)
                {
                    case PrimitiveType.Address:
                    case PrimitiveType.Hash160:
                        {
                            if (buffer.Length < UInt160.Length) throw new Exception();
                            value = new UInt160(buffer.Slice(0, UInt160.Length).Span);
                            buffer = buffer.Slice(UInt160.Length);
                            break;
                        }
                    case PrimitiveType.Hash256:
                        {
                            if (buffer.Length < UInt256.Length) throw new Exception();
                            value = new UInt256(buffer.Slice(0, UInt256.Length).Span);
                            buffer = buffer.Slice(UInt256.Length);
                            break;
                        }
                    case PrimitiveType.ByteArray:
                        {
                            // byte array only supported for final key segment
                            if (i != storageDef.KeySegments.Count - 1) throw new Exception();
                            value = buffer.ToArray();
                            break;
                        }
                    case PrimitiveType.String:
                        {
                            // string only supported for final key segment
                            if (i != storageDef.KeySegments.Count - 1) throw new Exception();
                            value = Neo.Utility.StrictUTF8.GetString(buffer.Span);
                            break;
                        }
                    case PrimitiveType.Integer:
                        {
                            // Integer only supported for final key segment
                            if (i != storageDef.KeySegments.Count - 1) throw new Exception();
                            value = new BigInteger(buffer.Span);
                            break;
                        }
                    default:
                        {
                            throw new NotImplementedException($"{segment.Type}");
                        }
                }

                yield return new KeySegment(segment.Name, segment.Type, value);
            }
        }

        public static string AsAddress(this UInt160 @this, byte addressVersion)
        {
            var address = Neo.Wallets.Helper.ToAddress(@this, addressVersion);

            // Note: Zero is not a valid Neo address character. So use 
            //       "000000000000000000000000000000000" to represent UInt160.Zero

            return @this == UInt160.Zero
                ? $"{address[0]}000000000000000000000000000000000"
                : address;
        }

        public static UInt160 FromAddress(this ReadOnlyMemory<char> @this, byte addressVersion)
        {
            if (@this.IsEmpty) throw new FormatException();

            if (@this.Span.Slice(1).SequenceEqual("000000000000000000000000000000000"))
            {
                var address = Neo.Wallets.Helper.ToAddress(UInt160.Zero, addressVersion);
                if (address[0] != @this.Span[0]) throw new FormatException();
                return UInt160.Zero;
            }
            else
            {
                return Neo.Wallets.Helper.ToScriptHash(new string(@this.Span), addressVersion);
            }
        }

        public static ReadOnlyMemory<byte> FromHexString(this ReadOnlyMemory<char> @this)
        {
            if (@this.StartsWith("0x")) @this = @this.Slice(2);
            return Convert.FromHexString(@this.Span);
        }

        public static string AsValue(this ReadOnlyMemory<byte> @this, PrimitiveContractType type, byte addressVersion)
            => type.Type switch
            {
                PrimitiveType.Address => new UInt160(@this.Span).AsAddress(addressVersion),
                PrimitiveType.Boolean => new BigInteger(@this.Span) != 0 ? "true" : "false",
                PrimitiveType.ByteArray or PrimitiveType.Signature => @this.Span.ToHexString(),
                PrimitiveType.Hash160 => new UInt160(@this.Span).ToString(),
                PrimitiveType.Hash256 => new UInt256(@this.Span).ToString(),
                PrimitiveType.Integer => new BigInteger(@this.Span).ToString(),
                PrimitiveType.PublicKey => ECPoint.DecodePoint(@this.Span, ECCurve.Secp256r1).ToString(),
                PrimitiveType.String => Neo.Utility.StrictUTF8.GetString(@this.Span),
                _ => throw new NotSupportedException($"{type.Type} primitive type"),
            };

        public static Variable AsVariable(this StackItem item, IVariableManager manager, string name, ContractParameterType parameterType)
        {
            if (item.IsNull) return new Variable { Name = name, Value = "<null>", Type = $"{parameterType}" };

            return parameterType switch
            {
                ContractParameterType.Any => item.AsVariable(manager, name),
                ContractParameterType.Array => ConvertArray(item, manager, name),
                ContractParameterType.Boolean => new Variable(name, $"{item.GetBoolean()}", 0) { Type = $"{parameterType}" },
                ContractParameterType.ByteArray => ByteArrayContainer.Create(manager, item, name),
                ContractParameterType.Hash160 => new Variable(name, $"{new UInt160(item.GetSpan())}", 0) { Type = $"{parameterType}" },
                ContractParameterType.Hash256 => new Variable(name, $"{new UInt256(item.GetSpan())}", 0) { Type = $"{parameterType}" },
                ContractParameterType.Integer => new Variable(name, $"{item.GetInteger()}", 0) { Type = $"{parameterType}" },
                ContractParameterType.InteropInterface => ((Neo.VM.Types.InteropInterface)item).TryGetInteropType(out var interopType)
                    ? new Variable(name, $"InteropInterface<{interopType.Name}>", 0)
                    : new Variable(name, $"InteropInterface", 0),                
                ContractParameterType.Map => NeoMapContainer.Create(manager, (NeoMap)item, name),
                ContractParameterType.PublicKey => new Variable(name,$"{ECPoint.DecodePoint(item.GetSpan(), ECCurve.Secp256r1)}", 0) { Type = $"{parameterType}" },
                ContractParameterType.Signature => ByteArrayContainer.Create(manager, item, name),
                ContractParameterType.String => new Variable(name, item.GetString(), 0) { Type = $"{parameterType}" },
                // ContractParameterType.Void
                _ => throw new NotSupportedException($"AsVariable {parameterType} not supported"),
            };

            static Variable ConvertArray(StackItem item, IVariableManager manager, string name)
            {
                if (item is NeoArray array) return NeoArrayContainer.Create(manager, array, name);
                if (ByteArrayContainer.TryCreate(manager, item, name, out var variable)) return variable;
                throw new NotSupportedException($"Cannot convert {item.Type} to array variable");
            }
        }

        public static Variable AsVariable(this ReadOnlyMemory<byte> @this, IVariableManager manager, string name, PrimitiveContractType type, byte addressVersion)
        {
            if (type.Type == PrimitiveType.ByteArray)
            {
                var variable = ByteArrayContainer.Create(manager, @this, name);
                return variable;
            }
            else
            {
                return new Variable
                {
                    Name = name,
                    Value = @this.AsValue(type, addressVersion),
                    Type = type.AsTypeName(),
                };
            }
        }

        public static Variable AsVariable(this StackItem @this, IVariableManager manager, string name, ContractType type, byte addressVersion)
        {
            if (type is PrimitiveContractType primitive
                && @this is Neo.VM.Types.PrimitiveType)
            {
                if (primitive.Type == PrimitiveType.ByteArray
                    || primitive.Type == PrimitiveType.Signature)
                {
                    var byteString = (Neo.VM.Types.ByteString)@this.ConvertTo(Neo.VM.Types.StackItemType.ByteString);
                    var variable = ByteArrayContainer.Create(manager, byteString, name);
                    return variable;
                }

                string value = primitive.Type switch
                {
                    PrimitiveType.Address => new UInt160(@this.GetSpan()).AsAddress(addressVersion),
                    PrimitiveType.Boolean => $"{@this.GetBoolean()}",
                    // PrimitiveType.ByteArray handled above
                    PrimitiveType.Hash160 => $"{new UInt160(@this.GetSpan())}",
                    PrimitiveType.Hash256 => $"{new UInt256(@this.GetSpan())}",
                    PrimitiveType.Integer => $"{new BigInteger(@this.GetSpan())}",
                    PrimitiveType.PublicKey => $"{ECPoint.DecodePoint(@this.GetSpan(), ECCurve.Secp256r1)}",
                    // PrimitiveType.Signature handled above
                    PrimitiveType.String => @this.GetString() ?? "<null>",
                    _ => throw new NotSupportedException($"{primitive.Type}"),
                };

                return new Variable
                {
                    Name = name,
                    Value = value,
                    Type = type.AsTypeName()
                };
            }

            if (@this is NeoArray array
                && type is StructContractType structType
                && array.Count == structType.Fields.Count)
            {
                var variable = NeoArrayContainer.Create(manager, array, name, structType, addressVersion);
                return variable;
            }

            if (@this is NeoMap map
                && type is MapContractType mapType)
            {
                var variable = NeoMapContainer.Create(manager, map, name, mapType);
                return variable;
            }

            return @this.AsVariable(manager, name);
        }

        public static Variable AsVariable(this StackItem @this, IVariableManager manager, string name)
        {
            switch (@this)
            {
                // case Neo.VM.Types.Struct: break;
                case Neo.VM.Types.Array array: return NeoArrayContainer.Create(manager, array, name);
                case Neo.VM.Types.Boolean: return new Variable { Name = name, Value = $"{@this.GetBoolean()}", Type = "Boolean" }; ;
                case Neo.VM.Types.Buffer buffer: return ByteArrayContainer.Create(manager, buffer.InnerBuffer, name);
                case Neo.VM.Types.ByteString byteString: return ByteArrayContainer.Create(manager, byteString, name);
                case Neo.VM.Types.Integer: return new Variable { Name = name, Value = $"{@this.GetInteger()}", Type = "Integer" }; ;
                // case Neo.VM.Types.InteropInterface: break;
                case Neo.VM.Types.Map map: return NeoMapContainer.Create(manager, map, name);
                case Neo.VM.Types.Null: return new Variable { Name = name, Value = "<null>" };
                // case Neo.VM.Types.Pointer: break;
                default: throw new NotSupportedException($"StackItem {@this.Type}");
            }
            throw new Exception();

        }
    }
}
