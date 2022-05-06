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

namespace NeoDebug.Neo3
{
    readonly record struct KeySegment(string Name, PrimitiveType Type, object Value);

    static class SchematizedStorageExtensions
    {
        public static string AsString(this KeySegment @this, byte addressVersion)
            => @this.Type switch
            {
                PrimitiveType.Address when @this.Value is UInt160 uint160 => uint160.AsAddress(addressVersion),
                PrimitiveType.ByteArray when @this.Value is byte[] array => Convert.ToHexString(array),
                _ => $"{@this.Value}",
            };

        public static string AsTypeName(this ContractType type)
            => type switch
            {
                ArrayContractType arrayType => $"Array<{arrayType.Type.AsTypeName()}>",
                InteropContractType interopType => $"Interop<{interopType.Type}>",
                MapContractType mapType => $"Map<{mapType.KeyType}, {mapType.ValueType.AsTypeName()}>",
                PrimitiveContractType primitiveType => $"#{primitiveType.Type}",
                StructContractType structType => structType.ShortName,
                UnspecifiedContractType => "<unspecified>",
                _ => throw new ArgumentException(type.GetType().Name, nameof(type)),
            };

        public static IEnumerable<KeySegment> AsKeySegments(this ReadOnlyMemory<byte> buffer, StorageGroupDef storageGroupDef)
        {
            buffer = buffer.Slice(storageGroupDef.KeyPrefix.Length);

            for (int i = 0; i < storageGroupDef.KeySegments.Count; i++)
            {
                var segment = storageGroupDef.KeySegments[i];

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
                            if (i != storageGroupDef.KeySegments.Count - 1) throw new Exception();
                            value = buffer.ToArray();
                            buffer = default;
                            break;
                        }
                    case PrimitiveType.String:
                        {
                            // string only supported for final key segment
                            if (i != storageGroupDef.KeySegments.Count - 1) throw new Exception();
                            value = Neo.Utility.StrictUTF8.GetString(buffer.Span);
                            buffer = default;
                            break;
                        }
                    case PrimitiveType.Integer:
                        {
                            // Integer only supported for final key segment
                            if (i != storageGroupDef.KeySegments.Count - 1) throw new Exception();
                            value = new BigInteger(buffer.Span);
                            buffer = default;
                            break;
                        }
                    default:
                        {
                            throw new NotImplementedException($"{segment.Type}");
                        }
                }

                yield return new KeySegment(segment.Name, segment.Type, value);
            }

            if (!buffer.IsEmpty) throw new Exception("woops, key def wrong");
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

        public static UInt160 FromAddress(this ReadOnlySpan<char> @this, byte addressVersion)
        {
            if (@this.IsEmpty) throw new FormatException();

            if (@this.Slice(1).SequenceEqual("000000000000000000000000000000000"))
            {
                var address = Neo.Wallets.Helper.ToAddress(UInt160.Zero, addressVersion);
                if (address[0] != @this[0]) throw new FormatException();
                return UInt160.Zero;
            }
            else
            {
                return Neo.Wallets.Helper.ToScriptHash(new string(@this), addressVersion);
            }
        }

        public static ReadOnlyMemory<byte> FromHexString(this ReadOnlyMemory<char> @this)
        {
            if (@this.Span.StartsWith("0x")) @this = @this.Slice(2);
            return Convert.FromHexString(@this.Span);
        }

        public static string AsValue(this ReadOnlySpan<byte> @this, PrimitiveContractType type, byte addressVersion)
            => AsValue(@this, type.Type, addressVersion);

        public static string AsValue(this ReadOnlySpan<byte> @this, PrimitiveType type, byte addressVersion)
        {
            try
            {
                return type switch
                {
                    PrimitiveType.Address => new UInt160(@this).AsAddress(addressVersion),
                    PrimitiveType.Boolean => new BigInteger(@this) != 0 ? "true" : "false",
                    PrimitiveType.ByteArray or PrimitiveType.Signature => Convert.ToHexString(@this),
                    PrimitiveType.Hash160 => new UInt160(@this).ToString(),
                    PrimitiveType.Hash256 => new UInt256(@this).ToString(),
                    PrimitiveType.Integer => new BigInteger(@this).ToString(),
                    PrimitiveType.PublicKey => ECPoint.DecodePoint(@this, ECCurve.Secp256r1).ToString(),
                    PrimitiveType.String => Neo.Utility.StrictUTF8.GetString(@this),
                    _ => throw new NotSupportedException($"{type} primitive type"),
                };
            }
            catch (System.Exception ex) when (ex.GetType() != typeof(NotSupportedException))
            {
                return $"<invalid {type}>";
            }
        }

        public static Variable AsVariable(this ReadOnlyMemory<byte> @this, IVariableManager manager, string name, PrimitiveContractType type, byte addressVersion)
        {
            if (type.Type == PrimitiveType.ByteArray
                || type.Type == PrimitiveType.Signature)
            {
                var variable = ByteArrayContainer.Create(manager, @this, name);
                return variable;
            }
            else
            {
                return new Variable
                {
                    Name = name,
                    Value = @this.Span.AsValue(type, addressVersion),
                    Type = type.AsTypeName(),
                };
            }
        }

        public static Variable AsVariable(this StackItem @this, IVariableManager manager, string name, ContractType type, byte addressVersion)
        {
            if (type is UnspecifiedContractType) return AsVariable(@this, manager, name);

            if (@this is Neo.VM.Types.PrimitiveType
                && type is PrimitiveContractType primitive)
            {
                if (primitive.Type == PrimitiveType.ByteArray
                    || primitive.Type == PrimitiveType.Signature)
                {
                    var byteString = (Neo.VM.Types.ByteString)@this.ConvertTo(Neo.VM.Types.StackItemType.ByteString);
                    var variable = ByteArrayContainer.Create(manager, byteString, name);
                    return variable;
                }

                // ByteArray and Signature handled above
                System.Diagnostics.Debug.Assert(primitive.Type != PrimitiveType.ByteArray 
                    && primitive.Type != PrimitiveType.Signature);

                string value = primitive.Type switch
                {
                    PrimitiveType.Boolean => $"{@this.GetBoolean()}",
                    PrimitiveType.Integer => $"{@this.GetInteger()}",
                    PrimitiveType.String => @this.GetString() ?? "<null>",
                    PrimitiveType.Address 
                        or PrimitiveType.Hash160
                        or PrimitiveType.Hash256 
                        or PrimitiveType.PublicKey
                            => AsValue(@this.GetSpan(), primitive.Type, addressVersion),
                    _ => throw new NotSupportedException($"{primitive.Type}"),
                };

                return new Variable
                {
                    Name = name,
                    Value = value,
                    Type = type.AsTypeName()
                };
            }

            if (@this is Neo.VM.Types.Buffer buffer
                && type is PrimitiveContractType primitive2)
            {
                // TODO handle

            }

            if (@this is Neo.VM.Types.InteropInterface
                && type is InteropContractType interopType)
            {
                return new Variable
                {
                    Name = name,
                    Value = type.AsTypeName()
                };
            }

            if (@this is NeoArray array)
            {
                if (type is StructContractType structType
                    && array.Count == structType.Fields.Count)
                {
                    return NeoArrayContainer.Create(manager, array, name, structType, addressVersion);
                }

                if (type is ArrayContractType arrayType)
                {
                    return NeoArrayContainer.Create(manager, array, name, arrayType, addressVersion);
                }
            }

            if (@this is NeoMap map
                && type is MapContractType mapType)
            {
                return NeoMapContainer.Create(manager, map, name, mapType, addressVersion);
            }

            {
                var variable = @this.AsVariable(manager, name);
                variable.Type = type.AsTypeName();
                return variable;
            }
        }

        public static Variable AsVariable(this StackItem @this, IVariableManager manager, string name)
        {
            switch (@this)
            {
                // case Neo.VM.Types.Struct: break;
                case Neo.VM.Types.Array array:
                    return NeoArrayContainer.Create(manager, array, name);
                case Neo.VM.Types.Boolean:
                    return new Variable { Name = name, Value = $"{@this.GetBoolean()}", Type = "Boolean" }; ;
                case Neo.VM.Types.Buffer buffer:
                    return ByteArrayContainer.Create(manager, buffer.InnerBuffer, name);
                case Neo.VM.Types.ByteString byteString:
                    return ByteArrayContainer.Create(manager, byteString, name);
                case Neo.VM.Types.Integer:
                    return new Variable { Name = name, Value = $"{@this.GetInteger()}", Type = "Integer" }; ;
                case Neo.VM.Types.InteropInterface:
                    return new Variable { Name = name, Value = "Interop<unspecified>" }; ;
                case Neo.VM.Types.Map map:
                    return NeoMapContainer.Create(manager, map, name);
                case Neo.VM.Types.Null:
                    return new Variable { Name = name, Value = "<null>" };
                case Neo.VM.Types.Pointer pointer:
                    // TODO: decode the pointer.Script value
                    return new Variable { Name = name, Value = $"Pointer<{pointer.Position}>", Type = "Pointer" }; ;
                default:
                    throw new NotSupportedException($"StackItem {@this.Type}");
            }
            throw new Exception();

        }
    }
}
