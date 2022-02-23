// using System;
// using System.Collections.Generic;
// using System.Numerics;
// using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
// using Neo;
// using Neo.BlockchainToolkit.Models;
// using Neo.SmartContract;

using StackItem = Neo.VM.Types.StackItem;
using NeoArray = Neo.VM.Types.Array;
using NeoMap = Neo.VM.Types.Map;
// using Neo.Cryptography.ECC;

using System;
using System.Collections.Generic;
using System.Numerics;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo;
using Neo.BlockchainToolkit.Models;
using Neo.Cryptography.ECC;

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
        //         // public static Variable AsHexOrByteArrayVariable(this ReadOnlyMemory<byte> buffer, IVariableManager manager, string name, string type = "ByteArray")
        //         // {
        //         //     if (buffer.Length < 35)
        //         //     {
        //         //         return new Variable
        //         //         {
        //         //             Name = name,
        //         //             Value = "0x" + buffer.Span.ToHexString(),
        //         //             Type = $"{type}[{buffer.Length}]",
        //         //             // EvaluateName = prefix + name,
        //         //         };
        //         //     }
        //         //     else
        //         //     {
        //         //         var valueItem = ByteArrayContainer.Create(manager, buffer, name, type);
        //         //         // valueItem.EvaluateName = prefix + name;
        //         //         return valueItem;
        //         //     }
        //         // }

        //         public static Variable AsVariable(this StorageItem @this, IVariableManager manager, string name, ContractType type)
        //         {
        //             if (type is PrimitiveContractType primitiveType)
        //             {
        //                 if (primitiveType.Type == PrimitiveType.ByteArray)
        //                 {
        //                     var variable = ByteArrayContainer.Create(manager, @this.Value, name, "ByteArray");
        //                     return variable;
        //                 }
        //                 else
        //                 {
        //                     return AsVariable(@this.Value, manager, name, primitiveType);
        //                 }
        //             }
        //             else
        //             {
        //                 var stackItem = BinarySerializer.Deserialize(@this.Value, Neo.VM.ExecutionEngineLimits.Default);
        //                 if (stackItem is NeoArray array
        //                     && type is StructContractType structType
        //                     && array.Count == structType.Fields.Count)
        //                 {
        //                     var variable = NeoArrayContainer.Create(manager, array, name, structType);
        //                     return variable;
        //                 }

        //                 if (stackItem is NeoMap map && type is MapContractType mapType)
        //                 {
        //                     var variable = NeoMapContainer.Create(manager, map, name, mapType);
        //                     return variable;
        //                 }

        //                 return AsVariable(stackItem, manager, name, null);
        //             }
        //         }

        //         public static string AsValue(this ReadOnlyMemory<byte> @this, PrimitiveContractType type, byte? addressVersion = null)
        //             => type.Type switch
        //             {
        //                 PrimitiveType.Address => new UInt160(@this.Span).AsAddress(addressVersion),
        //                 PrimitiveType.Boolean => new BigInteger(@this.Span) != 0 ? "true" : "false",
        //                 PrimitiveType.ByteArray or PrimitiveType.Signature => @this.Span.ToHexString(),
        //                 PrimitiveType.Hash160 => new UInt160(@this.Span).ToString(),
        //                 PrimitiveType.Hash256 => new UInt256(@this.Span).ToString(),
        //                 PrimitiveType.Integer => new BigInteger(@this.Span).ToString(),
        //                 PrimitiveType.PublicKey => ECPoint.DecodePoint(@this.Span, ECCurve.Secp256r1).ToString(),
        //                 PrimitiveType.String => Neo.Utility.StrictUTF8.GetString(@this.Span),
        //                 _ => throw new NotSupportedException($"{type.Type} primitive type"),
        //             };

        //         public static Variable AsVariable(this ReadOnlyMemory<byte> @this, IVariableManager manager, string name, PrimitiveContractType type, byte? addressVersion = null)
        //         {
        //             if (type.Type == PrimitiveType.ByteArray)
        //             {
        //                 var variable = ByteArrayContainer.Create(manager, @this, name);
        //                 return variable;
        //             }
        //             else
        //             {
        //                 return new Variable
        //                 {
        //                     Name = name,
        //                     Value = @this.AsValue(type, addressVersion),
        //                     Type = type.AsTypeName(),
        //                 };
        //             }
        //         }

        //         public static Variable AsVariable(this StackItem @this, IVariableManager manager, string name, ContractType? type = null)
        //         {
        //             switch (@this)
        //             {
        //                 // case Neo.VM.Types.Struct: break;
        //                 case Neo.VM.Types.Array array: return NeoArrayContainer.Create(manager, array, name);
        //                 case Neo.VM.Types.Boolean: return new Variable { Name = name, Value = $"{@this.GetBoolean()}", Type = "Boolean" };;
        //                 case Neo.VM.Types.Buffer buffer: return ByteArrayContainer.Create(manager, buffer, name);
        //                 case Neo.VM.Types.ByteString byteString: return ByteArrayContainer.Create(manager, byteString, name);
        //                 case Neo.VM.Types.Integer: return new Variable { Name = name, Value = $"{@this.GetInteger()}", Type = "Integer" };;
        //                 // case Neo.VM.Types.InteropInterface: break;
        //                 case Neo.VM.Types.Map map: return NeoMapContainer.Create(manager, map, name);
        //                 case Neo.VM.Types.Null: return new Variable { Name = name, Value = "<null>" };
        //                 // case Neo.VM.Types.Pointer: break;
        //                 default: throw new NotSupportedException($"StackItem {@this.Type}");
        //             }
        //             throw new Exception();

        //         }
        // //         public static string AsEvaluateName(this StorageDef @this, IReadOnlyList<(string name, PrimitiveType type, object value)>? keySegments = null)
        //         {
        //             keySegments ??= Array.Empty<(string name, PrimitiveType type, object value)>();
        //             var builder = new System.Text.StringBuilder($"{DebugSession.STORAGE_PREFIX}.{@this.Name}");
        //             for (int i = 0; i < keySegments.Count; i++)
        //             {
        //                 builder.Append($"[{keySegments[i].value}]");
        //             }
        //             return builder.ToString();
        //         }

        //         public static string AsAddress(this UInt160 @this, byte? version = null)
        //         {
        //             var address = Neo.Wallets.Helper.ToAddress(@this, version ?? ProtocolSettings.Default.AddressVersion);

        //             // Note: Zero is not a valid neo address character. So use 
        //             //       "000000000000000000000000000000000" to represent UInt160.Zero

        //             return @this == UInt160.Zero
        //                 ? $"{address[0]}000000000000000000000000000000000"
        //                 : address;
        //         }

        //         public static string AsString(this (string name, PrimitiveType type, object value) @this, byte? version = null)
        //             => @this.type == PrimitiveType.Address && @this.value is UInt160 uint160
        //                 ? uint160.AsAddress(version) : $"{@this.value}";

        //         // public static string AsString(this ContractType typeDef)
        //         //     => throw new NotImplementedException(); // typeDef.Match(cpt => $"{cpt}", sd => sd.Name);

        //         // public static Variable AsVariable(this StackItem stackItem, IVariableManager manager, string name, ContractType typeDef, string evaluatePrefix)
        //         // {
        //         //     var variable = new Variable
        //         //     {
        //         //         Name = name,
        //         //         Value = " ",
        //         //         Type = typeDef.AsString(),
        //         //         EvaluateName = $"{evaluatePrefix}.{name}",
        //         //     };

        //         // throw new NotImplementedException();
        //         //     // if (typeDef.TryPickT0(out var cpt, out var structDef))
        //         //     // {
        //         //     //     variable.Value = stackItem.AsValue(cpt);
        //         //     // }
        //         //     // else
        //         //     // {
        //         //     //     if (stackItem is NeoArray array)
        //         //     //     {
        //         //     //         if (array.Count == structDef.Fields.Count)
        //         //     //         {
        //         //     //             var container = new SchematizedStructContainer(structDef, array, variable.EvaluateName);
        //         //     //             variable.VariablesReference = manager.Add(container);
        //         //     //             variable.NamedVariables = structDef.Fields.Count;
        //         //     //         }
        //         //     //         else
        //         //     //         {
        //         //     //             variable.Value = $"<storage item has {array.Count} fields, expected {structDef.Fields.Count}";
        //         //     //         }
        //         //     //     }
        //         //     //     else
        //         //     //     {
        //         //     //         variable.Value = $"<invalid storage item {stackItem.Type}";
        //         //     //     }
        //         //     // }

        //         //     // return variable;
        //         // }


        //         // public static Variable AsVariable(this StorageItem? storageItem, IVariableManager manager, string name, OneOf<PrimitiveType, StructDef> typeDef, string evaluatePrefix)
        //         // { 
        //         //     throw new NotImplementedException();
        //         //     // var variable = new Variable()
        //         //     // {
        //         //     //     Name = name,
        //         //     //     Value = string.Empty,
        //         //     //     Type = typeDef.AsString()
        //         //     // };

        //         //     // if (typeDef.TryPickT0(out var cpt, out var structDef))
        //         //     // {
        //         //     //     variable.Value = storageItem is null ? "<no value stored>" : storageItem.AsValue(cpt);
        //         //     //     variable.EvaluateName = $"{evaluatePrefix}.item";
        //         //     // }
        //         //     // else
        //         //     // {
        //         //     //     variable.Value = " ";
        //         //     //     variable.EvaluateName = $"{evaluatePrefix}.key";

        //         //     //     var stackItem = storageItem is null ? null : BinarySerializer.Deserialize(storageItem.Value, Neo.VM.ExecutionEngineLimits.Default);
        //         //     //     if (stackItem is null)
        //         //     //     {
        //         //     //         variable.Value = "<no value stored>";
        //         //     //     }
        //         //     //     else if (stackItem is NeoArray array)
        //         //     //     {
        //         //     //         if (array.Count == structDef.Fields.Count)
        //         //     //         {
        //         //     //             var container = new SchematizedStructContainer(structDef, array, $"{evaluatePrefix}.item");
        //         //     //             variable.VariablesReference = manager.Add(container);
        //         //     //             variable.NamedVariables = structDef.Fields.Count;
        //         //     //         }
        //         //     //         else
        //         //     //         {
        //         //     //             variable.Value = $"<storage item has {array.Count} fields, expected {structDef.Fields.Count}";
        //         //     //         }
        //         //     //     }
        //         //     //     else
        //         //     //     {
        //         //     //         variable.Value = $"<invalid storage item {stackItem.Type}";
        //         //     //     }

        //         //     // }

        //         //     // return variable;
        //         // }

        //         // public static string ToAddress(this UInt160 scriptHash, byte version) => Neo.Wallets.Helper.ToAddress(scriptHash, version);

        //         // public static string AsValue(this StackItem item, PrimitiveType cpt, byte? version = null)
        //         //     => throw new NotImplementedException(); 
        //         //     // cpt switch
        //         //     //     {
        //         //     //         PrimitiveStorageType.Boolean => $"{item.GetBoolean()}",
        //         //     //         PrimitiveStorageType.Hash160 => $"{new UInt160(item.GetSpan())}",
        //         //     //         PrimitiveStorageType.Address => $"{new UInt160(item.GetSpan()).AsAddress(version)}",
        //         //     //         PrimitiveStorageType.Hash256 => $"{new UInt256(item.GetSpan())}",
        //         //     //         PrimitiveStorageType.Integer => $"{item.GetInteger()}",
        //         //     //         PrimitiveStorageType.String => item.GetString() ?? string.Empty,
        //         //     //         PrimitiveStorageType.ByteArray => item.GetSpan().ToHexString(),
        //         //     //         _ => $"<{cpt} not implemented>"
        //         //     //     };

        //         // public static string AsValue(this StorageItem item, PrimitiveType cpt, byte? version = null)
        //         //     => throw new NotImplementedException(); 
        //         //     // cpt switch
        //         //     //     {
        //         //     //         PrimitiveStorageType.Boolean => $"{new BigInteger(item.Value) == 0}",
        //         //     //         PrimitiveStorageType.Hash160 => $"{new UInt160(item.Value)}",
        //         //     //         PrimitiveStorageType.Address => $"{new UInt160(item.Value).AsAddress(version)}",
        //         //     //         PrimitiveStorageType.Hash256 => $"{new UInt256(item.Value)}",
        //         //     //         PrimitiveStorageType.Integer => $"{new BigInteger(item.Value)}",
        //         //     //         PrimitiveStorageType.String => Neo.Utility.StrictUTF8.GetString(item.Value),
        //         //     //         PrimitiveStorageType.ByteArray => item.Value.ToHexString(),
        //         //     //         _ => $"<{cpt} not implemented>"
        //         //     //     };


    }
}
