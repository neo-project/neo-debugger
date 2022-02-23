using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo;
using Neo.BlockchainToolkit.Models;
using Neo.SmartContract;

using NeoArray = Neo.VM.Types.Array;
using NeoMap = Neo.VM.Types.Map;

namespace NeoDebug.Neo3
{
    using StackItem = Neo.VM.Types.StackItem;

    internal abstract class StorageContainerBase : IVariableContainer
    {
        readonly ContractStorageSchema schema;
        readonly byte addressVersion;

        protected StorageContainerBase(ContractStorageSchema schema, byte addressVersion)
        {
            this.schema = schema;
            this.addressVersion = addressVersion;
        }

        protected abstract IEnumerable<(ReadOnlyMemory<byte> key, StorageItem item)> GetStorages();

        public IEnumerable<Variable> Enumerate(IVariableManager manager)
        {
            if (schema.StorageDefs.Count > 0)
            {
                var storages = GetStorages().ToArray();
                return schema.StorageDefs
                    .Select(storageDef => CreateStorageVariable(manager, storageDef, storages))
                    .Append(new Variable()
                    {
                        Name = "#rawStorage",
                        Value = string.Empty,
                        VariablesReference = manager.Add(new RawStorageContainer(storages)),
                    });
            }
            else
            {
                return EnumerateRawStorage(manager, GetStorages());
            }
        }

        Variable CreateStorageVariable(IVariableManager manager, StorageDef storageDef, IEnumerable<(ReadOnlyMemory<byte> key, StorageItem item)> storages)
        {
            if (storageDef.KeySegments.Count == 0)
            {
                var (_, item) = storages.SingleOrDefault(s => s.key.Span.SequenceEqual(storageDef.KeyPrefix.Span));
                if (item is null)
                {
                    return new Variable
                    {
                        Name = storageDef.Name,
                        Type = storageDef.ValueType.AsTypeName(),
                        Value = "<null>",
                    };
                }
                else
                {
                    var variable = ToVariable(item, manager, storageDef, addressVersion);
                    variable.Name = storageDef.Name;
                    variable.Type = storageDef.ValueType.AsTypeName();
                    return variable;
                }
            }
            else
            {
                var storageItems = storages.Where(s => s.key.Span.StartsWith(storageDef.KeyPrefix.Span)).ToArray();
                var container = new SchematizedStorageContainer(storageDef, addressVersion, storageItems);
                var keyType = string.Join(", ", storageDef.KeySegments.Select(s => $"{s.Name}: {s.Type}"));
                return new Variable()
                {
                    Name = storageDef.Name,
                    Type = storageDef.ValueType.AsTypeName(),
                    Value = $"({keyType}) -> {storageDef.ValueType.AsTypeName()}[{storageItems.Length}]",
                    VariablesReference = manager.Add(container),
                    IndexedVariables = storageItems.Length,
                };
            }
        }

        static Variable ToVariable(StorageItem item, IVariableManager manager, StorageDef storageDef, byte addressVersion)
        {
            if (storageDef.ValueType is PrimitiveContractType primitiveType)
            {
                var memory = new ReadOnlyMemory<byte>(item.Value);
                var variable = memory.AsVariable(manager, storageDef.Name, primitiveType, addressVersion);
                return variable;
            }
            else
            {
                var stackItem = BinarySerializer.Deserialize(item.Value, Neo.VM.ExecutionEngineLimits.Default);
                var variable = stackItem.AsVariable(manager, storageDef.Name, storageDef.ValueType, addressVersion);
                return variable;
            }
        }

        static IEnumerable<Variable> EnumerateRawStorage(IVariableManager manager, IEnumerable<(ReadOnlyMemory<byte> key, StorageItem item)> storages)
        {
            foreach (var (key, item) in storages)
            {
                yield return new Variable()
                {
                    Name = key.Span.ToHexString(),
                    Value = string.Empty,
                    VariablesReference = manager.Add(new RawKeyItemContainer(key, item)),
                    NamedVariables = 2
                };
            }
        }

        public bool TryEvaluate(ReadOnlyMemory<char> expression, [MaybeNullWhen(false)] out StackItem result, out ReadOnlyMemory<char> remaining)
        {
            result = default;
            remaining = default;
            return false;
            // StorageDef storageDef = default;
            // if (expression.StartsWith(DebugSession.STORAGE_PREFIX))
            // {
            //     expression = expression.Slice(DebugSession.STORAGE_PREFIX.Length);
            //     if (!expression.IsEmpty && expression.Span[0] == '.' && schema is not null)
            //     {
            //         expression = expression.Slice(1);

            //         for (int i = 0; i < schema.StorageDefs.Count; i++)
            //         {
            //             if (expression.StartsWith(schema.StorageDefs[i].Name))
            //             {
            //                 storageDef = schema.StorageDefs[i];
            //                 break;
            //             }
            //         }


            //     }
            //     else if (!expression.IsEmpty && expression.Span[0] == '[')
            //     {
            //         expression = expression.Slice(1);
            //         var bracketIndex = expression.Span.IndexOf(']');
            //         if (bracketIndex != -1)
            //         {
            //             remaining = expression.Slice(bracketIndex + 1);
            //             if (remaining.StartsWith(".key") || remaining.StartsWith(".item"))
            //             {
            //                 expression = expression.Slice(0, bracketIndex);
            //                 if (expression.Span.StartsWith("0x")) expression = expression.Slice(2);
            //                 try
            //                 {
            //                     var key = Convert.FromHexString(expression.Span);
            //                 }
            //                 catch { }
            //             }
            //         }
            //     }
            // }

            // result = default;
            // remaining = default;
            // return false;

            // bool TryParseStorageExpression(ReadOnlyMemory<char> expression, ContractStorageSchema schema, out StorageDef storageDef, out ReadOnlyMemory<byte> key, out ReadOnlyMemory<char> remainder)
            // {
            //     key = default;
            //     remainder = default;
            //     storageDef = default;

            //     if (!expression.StartsWith($"{DebugSession.STORAGE_PREFIX}.")) return false;
            //     expression = expression.Slice(DebugSession.STORAGE_PREFIX.Length + 1);

            //     for (int i = 0; i < schema.StorageDefs.Count; i++)
            //     {
            //         if (expression.StartsWith(schema.StorageDefs[i].Name))
            //         {
            //             storageDef = schema.StorageDefs[i];
            //             break;
            //         }
            //     }

            //     if (string.IsNullOrEmpty(storageDef.Name)) return false;
            //     expression = expression.Slice(storageDef.Name.Length);

            //     if (storageDef.KeySegments is null) return false;

            //     var buffer = new ArrayBufferWriter<byte>();
            //     buffer.Write(storageDef.KeyPrefix.Span);

            //     foreach (var segment in storageDef.KeySegments)
            //     {
            //         if (expression.Span[0] != '[') return false;
            //         var index = expression.Span.IndexOf(']');
            //         if (index == -1) return false;
            //         var segmentValue = expression.Slice(1, index - 1);
            //         switch (segment.Type)
            //         {
            //             // case PrimitiveStorageType.Address: // TODO: address sting 
            //             // case PrimitiveStorageType.Hash160:
            //             //     {
            //             //         var hash = UInt160.Parse(segmentValue.ToString());
            //             //         buffer.Write(Neo.IO.Helper.ToArray(hash));
            //             //         break;
            //             //     }
            //             // case PrimitiveStorageType.Hash256:
            //             //     {
            //             //         var hash = UInt256.Parse(segmentValue.ToString());
            //             //         buffer.Write(Neo.IO.Helper.ToArray(hash));
            //             //         break;
            //             //     }
            //             // case PrimitiveStorageType.Integer:
            //             //     {
            //             //         var value = BigInteger.Parse(segmentValue.Span);
            //             //         buffer.Write(value.ToByteArray());
            //             //         break;

            //             //     }
            //             default: throw new NotImplementedException();
            //         }
            //         // expression = expression.Slice(index + 1);
            //     }

            //     key = buffer.WrittenMemory;
            //     remainder = expression;
            //     return true;
            // }

            // static bool TryParseStorageExpression(ReadOnlyMemory<char> expression, out ReadOnlyMemory<byte> key, out ReadOnlyMemory<char> remainder)
            // {
            //     remainder = default;
            //     key = default;
            //     if (!expression.StartsWith($"{DebugSession.STORAGE_PREFIX}[")) return false;
            //     expression = expression.Slice(DebugSession.STORAGE_PREFIX.Length + 1);
            //     var bracketIndex = expression.Span.IndexOf(']');
            //     if (bracketIndex == -1) return false;
            //     remainder = expression.Slice(bracketIndex + 1);
            //     if (!remainder.StartsWith(".key") && !remainder.StartsWith(".item")) return false;
            //     expression = expression.Slice(0, bracketIndex);
            //     if (expression.Span.StartsWith("0x")) expression = expression.Slice(2);
            //     try
            //     {
            //         key = Convert.FromHexString(expression.Span);
            //         return true;
            //     }
            //     catch { return false; }
            // }
            // public (StackItem? item, ReadOnlyMemory<char> remaining) Evaluate(ReadOnlyMemory<char> expression)
            // {
            //     if (schema is not null)
            //     {
            //         if (TryParseStorageExpression(expression, schema, out var storageDef, out var key, out var remainder))
            //         {
            //             if (remainder.Span.StartsWith(".key"))
            //             {
            //                 return (key, remainder.Slice(4));
            //             }

            //             else if (remainder.Span.StartsWith(".item"))
            //             {
            //                 remainder = remainder.Slice(5);
            //                 if (TryFindStorageItem(GetStorages(), key, out var item))
            //                 {

            //                     return (item.Value, remainder);
            //                 }
            //             }

            //         }
            //     }
            //     {
            //         if (TryParseStorageExpression(expression, out var key, out var remainder))
            //         {
            //             if (remainder.Span.StartsWith(".key"))
            //             {
            //                 return (key, remainder.Slice(4));
            //             }
            //             else if (remainder.Span.StartsWith(".item"))
            //             {
            //                 if (TryFindStorageItem(GetStorages(), key, out var item))
            //                 {
            //                     return (item.Value, remainder.Slice(5));
            //                 }
            //             }
            //         }
            //     }

            //     throw new InvalidOperationException("Invalid storage evaluation");



            //     static bool TryFindStorageItem(IEnumerable<(ReadOnlyMemory<byte> key, StorageItem item)> storages, ReadOnlyMemory<byte> key, [MaybeNullWhen(false)] out StorageItem item)
            //     {
            //         foreach (var storage in storages)
            //         {
            //             if (key.Span.SequenceEqual(storage.key.Span))
            //             {
            //                 item = storage.item;
            //                 return true;
            //             }
            //         }

            //         item = default;
            //         return false;
            //     }
            // }
        }

        class RawStorageContainer : IVariableContainer
        {
            readonly IEnumerable<(ReadOnlyMemory<byte> key, StorageItem item)> storages;

            public RawStorageContainer(IReadOnlyList<(ReadOnlyMemory<byte> key, StorageItem item)> storages)
            {
                this.storages = storages;
            }

            public IEnumerable<Variable> Enumerate(IVariableManager manager)
            {
                return EnumerateRawStorage(manager, storages);
            }
        }

        class RawKeyItemContainer : IVariableContainer
        {
            private readonly ReadOnlyMemory<byte> key;
            private readonly StorageItem item;
            private readonly string prefix;

            public RawKeyItemContainer(ReadOnlyMemory<byte> key, StorageItem item)
            {
                this.key = key;
                this.item = item;
                this.prefix = $"{DebugSession.STORAGE_PREFIX}[0x{key.Span.ToHexString()}].";
            }

            public IEnumerable<Variable> Enumerate(IVariableManager manager)
            {
                yield return CreateVariable(manager, key, "key");
                yield return CreateVariable(manager, new ReadOnlyMemory<byte>(item.Value), "item");
            }

            Variable CreateVariable(IVariableManager manager, ReadOnlyMemory<byte> buffer, string name)
            {
                var variable = ByteArrayContainer.Create(manager, buffer, name);
                // valueItem.EvaluateName = prefix + name;
                return variable;
            }
        }

        class SchematizedStorageContainer : IVariableContainer
        {
            readonly StorageDef storageDef;
            readonly byte addressVersion;
            readonly IReadOnlyList<(ReadOnlyMemory<byte> key, StorageItem item)> storages;

            public SchematizedStorageContainer(StorageDef storageDef, byte addressVersion, IReadOnlyList<(ReadOnlyMemory<byte> key, StorageItem item)> storages)
            {
                this.storageDef = storageDef;
                this.addressVersion = addressVersion;
                this.storages = storages;
            }

            public IEnumerable<Variable> Enumerate(IVariableManager manager)
            {
                for (int i = 0; i < storages.Count; i++)
                {
                    var (key, item) = storages[i];
                    var segments = key.AsKeySegments(storageDef).ToArray();
                    if (segments.Length == 1)
                    {
                        var variable = ToVariable(item, manager, storageDef, addressVersion);
                        variable.Name = segments[0].AsString(addressVersion);
                        variable.Type = storageDef.ValueType.AsTypeName();
                        yield return variable;
                    }
                    else
                    {
                        var container = new KeyItemContainer(segments, item, storageDef, addressVersion);
                        var variable = new Variable()
                        {
                            Name = $"{i}",
                            Value = string.Empty,
                            VariablesReference = manager.Add(container),
                            NamedVariables = 2,
                        };
                        yield return variable;
                    }
                }
            }
        }

        class KeyItemContainer : IVariableContainer
        {
            readonly IReadOnlyList<KeySegment> keySegments;
            readonly StorageItem storageItem;
            readonly StorageDef storageDef;
            readonly byte addressVersion;

            public KeyItemContainer(IReadOnlyList<KeySegment> keySegments, StorageItem storageItem, StorageDef storageDef, byte addressVersion)
            {
                this.keySegments = keySegments;
                this.storageItem = storageItem;
                this.storageDef = storageDef;
                this.addressVersion = addressVersion;
            }

            public IEnumerable<Variable> Enumerate(IVariableManager manager)
            {
                var keyContainer = new KeySegmentsContainer(keySegments, addressVersion);
                yield return new Variable()
                {
                    Name = "key",
                    Value = " ",
                    VariablesReference = manager.Add(keyContainer),
                    NamedVariables = keySegments.Count,
                    // EvaluateName = storageDef.AsEvaluateName(keySegments) + ".key",
                };

                var variable = ToVariable(storageItem, manager, storageDef, addressVersion);
                variable.Name = "item";
                yield return variable;
            }
        }

        class KeySegmentsContainer : IVariableContainer
        {
            readonly IReadOnlyList<KeySegment> keySegments;
            readonly byte addressVersion;

            public KeySegmentsContainer(IReadOnlyList<KeySegment> keySegments, byte addressVersion)
            {
                this.keySegments = keySegments;
                this.addressVersion = addressVersion;
            }

            public IEnumerable<Variable> Enumerate(IVariableManager manager)
            {
                foreach (var segment in keySegments)
                {
                    yield return new Variable
                    {
                        Name = segment.Name,
                        Value = segment.AsString(addressVersion),
                        Type = $"{segment.Type}"
                    };
                }
            }
        }

    }
}
