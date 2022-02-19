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
    static class SchematizedStorageExtensions
    {
        public static string AsString(this OneOf<ContractParameterType, StructDef> typeDef)
            => typeDef.Match(cpt => $"{cpt}", sd => sd.Name);

        public static Variable AsVariable(this StackItem stackItem, IVariableManager manager, string name, OneOf<ContractParameterType, StructDef> typeDef)
        {
            var variable = new Variable
            {
                Name = name,
                Value = string.Empty,
                Type = typeDef.AsString(),
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
                        var container = new SchematizedStructContainer(structDef, array);
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

        public static Variable AsVariable(this StorageItem? storageItem, IVariableManager manager, string name, OneOf<ContractParameterType, StructDef> typeDef)
        {
            var variable = new Variable()
            {
                Name = name,
                Value = string.Empty,
                Type = typeDef.AsString()
            };

            if (storageItem is null)
            {
                variable.Value = "<no value stored>";
            }
            else
            {
                if (typeDef.TryPickT0(out var cpt, out var structDef))
                {
                    variable.Value = storageItem.AsValue(cpt);
                }
                else
                {
                    var stackItem = BinarySerializer.Deserialize(storageItem.Value, Neo.VM.ExecutionEngineLimits.Default);
                    if (stackItem is NeoArray array)
                    {
                        if (array.Count == structDef.Fields.Count)
                        {
                            var container = new SchematizedStructContainer(structDef, array);
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
            }

            return variable;
        }

        public static string AsValue(this StackItem item, ContractParameterType cpt)
            => cpt switch
                {
                    ContractParameterType.Boolean => $"{item.GetBoolean()}",
                    ContractParameterType.Hash160 => $"{new UInt160(item.GetSpan())}",
                    ContractParameterType.Hash256 => $"{new UInt256(item.GetSpan())}",
                    ContractParameterType.Integer => $"{item.GetInteger()}",
                    ContractParameterType.String => item.GetString() ?? string.Empty,
                    ContractParameterType.ByteArray => item.GetSpan().ToHexString(),
                    _ => $"<{cpt} not implemented>"
                };

        public static string AsValue(this StorageItem item, ContractParameterType cpt)
            => cpt switch
                {
                    ContractParameterType.Boolean => $"{new BigInteger(item.Value) == 0}",
                    ContractParameterType.Hash160 => $"{new UInt160(item.Value)}",
                    ContractParameterType.Hash256 => $"{new UInt256(item.Value)}",
                    ContractParameterType.Integer => $"{new BigInteger(item.Value)}",
                    ContractParameterType.String => Neo.Utility.StrictUTF8.GetString(item.Value),
                    ContractParameterType.ByteArray => item.Value.ToHexString(),
                    _ => $"<{cpt} not implemented>"
                };

        public static IEnumerable<(string name, ContractParameter param)> AsKeySegments(this ReadOnlyMemory<byte> buffer, StorageDef storageDef)
        {
            buffer = buffer.Slice(storageDef.KeyPrefix.Length);

            for (int i = 0; i < storageDef.KeySegments.Count; i++)
            {
                var segment = storageDef.KeySegments[i];

                object value;
                switch (segment.Type)
                {
                    case ContractParameterType.Hash160:
                    {
                        if (buffer.Length < UInt160.Length) throw new Exception();
                        value = new UInt160(buffer.Slice(0, UInt160.Length).Span);
                        buffer = buffer.Slice(UInt160.Length);
                        break;
                    }
                    case ContractParameterType.Hash256:
                    {
                        if (buffer.Length < UInt256.Length) throw new Exception();
                        value = new UInt256(buffer.Slice(0, UInt256.Length).Span);
                        buffer = buffer.Slice(UInt256.Length);
                        break;
                    }
                    case ContractParameterType.ByteArray:
                    {
                        // byte array only supported for final key segment
                        if (i != storageDef.KeySegments.Count - 1) throw new Exception();
                        value = buffer.ToArray();
                        break;
                    }
                    case ContractParameterType.String:
                    {
                        // string only supported for final key segment
                        if (i != storageDef.KeySegments.Count - 1) throw new Exception();
                        value = Neo.Utility.StrictUTF8.GetString(buffer.Span);
                        break;
                    }
                    case ContractParameterType.Integer:
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

                var param = new ContractParameter { Type = segment.Type, Value = value };
                yield return (segment.Name, param);
            }
        }
    }
}
