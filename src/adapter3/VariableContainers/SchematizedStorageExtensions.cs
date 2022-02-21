using System;
using System.Collections.Generic;
using System.Numerics;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo;
using Neo.BlockchainToolkit;
using Neo.SmartContract;
using OneOf;

using StackItem = Neo.VM.Types.StackItem;
using NeoArray = Neo.VM.Types.Array;

namespace NeoDebug.Neo3
{
    using StorageValueTypeDef = OneOf<PrimitiveStorageType, StructDef>;

    static class SchematizedStorageExtensions
    {
        public static string AsEvaluateName(this StorageDef @this, IReadOnlyList<(string name, PrimitiveStorageType type, object value)>? keySegments = null)
        {
            keySegments ??= Array.Empty<(string name, PrimitiveStorageType type, object value)>();
            var builder = new System.Text.StringBuilder($"{DebugSession.STORAGE_PREFIX}.{@this.Name}");
            for (int i = 0; i < keySegments.Count; i++)
            {
                builder.Append($"[{keySegments[i].value}]");
            }
            return builder.ToString();
        }

        public static string AsAddress(this UInt160 @this, byte? version = null)
        {
            if (@this == UInt160.Zero) return "<zero address>";

            return Neo.Wallets.Helper.ToAddress(@this, 
                version ?? ProtocolSettings.Default.AddressVersion);
        }

        public static string AsString(this (string name, PrimitiveStorageType type, object value) @this, byte? version = null)
            => @this.type == PrimitiveStorageType.Address && @this.value is UInt160 uint160
                ? uint160.AsAddress(version) : $"{@this.value}";

        public static string AsString(this StorageValueTypeDef typeDef)
            => typeDef.Match(cpt => $"{cpt}", sd => sd.Name);

        public static Variable AsVariable(this StackItem stackItem, IVariableManager manager, string name, StorageValueTypeDef typeDef, string evaluatePrefix)
        {
            var variable = new Variable
            {
                Name = name,
                Value = " ",
                Type = typeDef.AsString(),
                EvaluateName = $"{evaluatePrefix}.{name}",
            };

            if (typeDef.TryPickT0(out var cpt, out var structDef))
            {
                variable.Value = stackItem.AsValue(cpt);
            }
            else
            {
                if (stackItem is NeoArray array)
                {
                    if (array.Count == structDef.Fields.Count)
                    {
                        var container = new SchematizedStructContainer(structDef, array, variable.EvaluateName);
                        variable.VariablesReference = manager.Add(container);
                        variable.NamedVariables = structDef.Fields.Count;
                    }
                    else
                    {
                        variable.Value = $"<storage item has {array.Count} fields, expected {structDef.Fields.Count}";
                    }
                }
                else
                {
                    variable.Value = $"<invalid storage item {stackItem.Type}";
                }
            }

            return variable;
        }

        public static Variable AsVariable(this StorageItem? storageItem, IVariableManager manager, string name, OneOf<PrimitiveStorageType, StructDef> typeDef, string evaluatePrefix)
        {
            var variable = new Variable()
            {
                Name = name,
                Value = string.Empty,
                Type = typeDef.AsString()
            };

            if (typeDef.TryPickT0(out var cpt, out var structDef))
            {
                variable.Value = storageItem is null ? "<no value stored>" : storageItem.AsValue(cpt);
                variable.EvaluateName = $"{evaluatePrefix}.item";
            }
            else
            {
                variable.Value = " ";
                variable.EvaluateName = $"{evaluatePrefix}.key";

                var stackItem = storageItem is null ? null : BinarySerializer.Deserialize(storageItem.Value, Neo.VM.ExecutionEngineLimits.Default);
                if (stackItem is null)
                {
                    variable.Value = "<no value stored>";
                }
                else if (stackItem is NeoArray array)
                {
                    if (array.Count == structDef.Fields.Count)
                    {
                        var container = new SchematizedStructContainer(structDef, array, $"{evaluatePrefix}.item");
                        variable.VariablesReference = manager.Add(container);
                        variable.NamedVariables = structDef.Fields.Count;
                    }
                    else
                    {
                        variable.Value = $"<storage item has {array.Count} fields, expected {structDef.Fields.Count}";
                    }
                }
                else
                {
                    variable.Value = $"<invalid storage item {stackItem.Type}";
                }

            }

            return variable;
        }

        public static string ToAddress(this UInt160 scriptHash, byte version) => Neo.Wallets.Helper.ToAddress(scriptHash, version);

        public static string AsValue(this StackItem item, PrimitiveStorageType cpt, byte? version = null)
            => cpt switch
                {
                    PrimitiveStorageType.Boolean => $"{item.GetBoolean()}",
                    PrimitiveStorageType.Hash160 => $"{new UInt160(item.GetSpan())}",
                    PrimitiveStorageType.Address => $"{new UInt160(item.GetSpan()).AsAddress(version)}",
                    PrimitiveStorageType.Hash256 => $"{new UInt256(item.GetSpan())}",
                    PrimitiveStorageType.Integer => $"{item.GetInteger()}",
                    PrimitiveStorageType.String => item.GetString() ?? string.Empty,
                    PrimitiveStorageType.ByteArray => item.GetSpan().ToHexString(),
                    _ => $"<{cpt} not implemented>"
                };

        public static string AsValue(this StorageItem item, PrimitiveStorageType cpt, byte? version = null)
            => cpt switch
                {
                    PrimitiveStorageType.Boolean => $"{new BigInteger(item.Value) == 0}",
                    PrimitiveStorageType.Hash160 => $"{new UInt160(item.Value)}",
                    PrimitiveStorageType.Address => $"{new UInt160(item.Value).AsAddress(version)}",
                    PrimitiveStorageType.Hash256 => $"{new UInt256(item.Value)}",
                    PrimitiveStorageType.Integer => $"{new BigInteger(item.Value)}",
                    PrimitiveStorageType.String => Neo.Utility.StrictUTF8.GetString(item.Value),
                    PrimitiveStorageType.ByteArray => item.Value.ToHexString(),
                    _ => $"<{cpt} not implemented>"
                };

        public static IEnumerable<(string name, PrimitiveStorageType type, object value)> AsKeySegments(this ReadOnlyMemory<byte> buffer, StorageDef storageDef)
        {
            buffer = buffer.Slice(storageDef.KeyPrefix.Length);

            for (int i = 0; i < storageDef.KeySegments.Count; i++)
            {
                var segment = storageDef.KeySegments[i];

                object value;
                switch (segment.Type)
                {
                    case PrimitiveStorageType.Address:
                    case PrimitiveStorageType.Hash160:
                    {
                        if (buffer.Length < UInt160.Length) throw new Exception();
                        value = new UInt160(buffer.Slice(0, UInt160.Length).Span);
                        buffer = buffer.Slice(UInt160.Length);
                        break;
                    }
                    case PrimitiveStorageType.Hash256:
                    {
                        if (buffer.Length < UInt256.Length) throw new Exception();
                        value = new UInt256(buffer.Slice(0, UInt256.Length).Span);
                        buffer = buffer.Slice(UInt256.Length);
                        break;
                    }
                    case PrimitiveStorageType.ByteArray:
                    {
                        // byte array only supported for final key segment
                        if (i != storageDef.KeySegments.Count - 1) throw new Exception();
                        value = buffer.ToArray();
                        break;
                    }
                    case PrimitiveStorageType.String:
                    {
                        // string only supported for final key segment
                        if (i != storageDef.KeySegments.Count - 1) throw new Exception();
                        value = Neo.Utility.StrictUTF8.GetString(buffer.Span);
                        break;
                    }
                    case PrimitiveStorageType.Integer:
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

                yield return (segment.Name, segment.Type, value);
            }
        }
    }
}
