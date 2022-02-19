using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo;
using Neo.BlockchainToolkit;
using Neo.SmartContract;
using OneOf;

namespace NeoDebug.Neo3
{
    using Storages = IEnumerable<(ReadOnlyMemory<byte> key, StorageItem item)>;
    using StoragesList = IReadOnlyList<(ReadOnlyMemory<byte> key, StorageItem item)>;

    static class UtilityExtensions
    {
        public static string AsString(this OneOf<ContractParameterType, StructDef> typeDef)
            => typeDef.Match(cpt => $"{cpt}", sd => sd.Name);

        public static Variable AsVariable(this Neo.VM.Types.StackItem stackItem, IVariableManager manager, string name, OneOf<ContractParameterType, StructDef> typeDef)
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
                if (stackItem is Neo.VM.Types.Array array)
                {
                    if (array.Count == structDef.Fields.Count)
                    {
                        var container = new StructContainer(structDef, array);
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
                    if (stackItem is Neo.VM.Types.Array array)
                    {
                        if (array.Count == structDef.Fields.Count)
                        {
                            var container = new StructContainer(structDef, array);
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

        public static string AsValue(this Neo.VM.Types.StackItem item, ContractParameterType cpt)
            => cpt switch
                {
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

    internal class StructContainer : IVariableContainer
    {
        readonly StructDef @struct;
        readonly Neo.VM.Types.Array item;

        public StructContainer(StructDef @struct, Neo.VM.Types.Array item)
        {
            if (@struct.Fields.Count != item.Count) throw new ArgumentException();

            this.@struct = @struct;
            this.item = item;
        }

        public IEnumerable<Variable> Enumerate(IVariableManager manager)
        {
            for (int i = 0; i < @struct.Fields.Count; i++)
            {
                var (name, type) = @struct.Fields[i];
                var value = item[i];
                yield return value.AsVariable(manager, name, type);
            }
        }
    }

    internal class SchematizedKeyContainer : IVariableContainer
    {
        readonly IReadOnlyList<(string name, ContractParameter param)> segments;

        public SchematizedKeyContainer(IReadOnlyList<(string name, ContractParameter param)> segments)
        {
            this.segments = segments;
        }

        public IEnumerable<Variable> Enumerate(IVariableManager manager)
        {
            foreach (var segment in segments)
            {
                yield return new Variable
                {
                    Name = segment.name,
                    Value = $"{segment.param.Value}",
                    Type = $"{segment.param.Type}"
                };
            }
        }
    }

    internal class SchematizedStorageItemContainer : IVariableContainer
    {
        readonly StorageDef storageDef;
        readonly ReadOnlyMemory<byte> key;
        readonly StorageItem item;

        public SchematizedStorageItemContainer(StorageDef storageDef, ReadOnlyMemory<byte> key, StorageItem item)
        {
            if (storageDef.KeySegments.Count <= 0) throw new ArgumentException(nameof(storageDef));

            this.storageDef = storageDef;
            this.key = key;
            this.item = item;
        }

        public IEnumerable<Variable> Enumerate(IVariableManager manager)
        {
            var keySegments = key.AsKeySegments(storageDef).ToArray();
            var keyContainer = new SchematizedKeyContainer(keySegments);
            yield return new Variable()
            {
                Name = "key",
                Value = string.Empty,
                VariablesReference = manager.Add(keyContainer),
                NamedVariables = keySegments.Length
            };

            yield return item.AsVariable(manager, "value", storageDef.Value);
        }
    }

    internal class SchematizedStoragesContainer : IVariableContainer
    {
        readonly StorageDef storageDef;
        readonly StoragesList storages;

        public SchematizedStoragesContainer(StorageDef storageDef, StoragesList storages)
        {
            this.storageDef = storageDef;
            this.storages = storages;
        }

        public static Variable Create(IVariableManager manager, StorageDef storageDef, Storages storages)
        {
            if (storageDef.KeySegments.Count == 0)
            {
                var (_, item) = storages.SingleOrDefault(s => s.key.Span.SequenceEqual(storageDef.KeyPrefix.Span));
                return item.AsVariable(manager, storageDef.Name, storageDef.Value);
            }
            else
            {
                var values = storages.Where(s => s.key.Span.StartsWith(storageDef.KeyPrefix.Span)).ToArray();
                var container = new SchematizedStoragesContainer(storageDef, values);
                return new Variable()
                {
                    Name = storageDef.Name,
                    Value = $"{storageDef.Value.AsString()}[{values.Length}]",
                    VariablesReference = manager.Add(container),
                    IndexedVariables = values.Length,
                };
            }
        }

        public IEnumerable<Variable> Enumerate(IVariableManager manager)
        {
            for (int i = 0; i < storages.Count; i++)
            {
                var (key, item) = storages[i];
                var segments = key.AsKeySegments(storageDef).ToArray();

                var container = new SchematizedStorageItemContainer(storageDef, key, item);
                yield return new Variable()
                {
                    Name = $"{i}",
                    Value = string.Empty,
                    VariablesReference = manager.Add(container),
                    NamedVariables = 2,
                };
            }
        }
    }
}
