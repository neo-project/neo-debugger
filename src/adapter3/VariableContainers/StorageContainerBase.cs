using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo;
using Neo.BlockchainToolkit;
using Neo.SmartContract;
using OneOf;

namespace NeoDebug.Neo3
{
    using StackItem = Neo.VM.Types.StackItem;
    using StorageValueTypeDef = OneOf<ContractParameterType, StructDef>;

    internal abstract class StorageContainerBase : IVariableContainer
    {
        readonly ContractStorageSchema? schema;

        protected StorageContainerBase(ContractStorageSchema? schema)
        {
            this.schema = schema;
        }

        protected abstract IEnumerable<(ReadOnlyMemory<byte> key, StorageItem item)> GetStorages();

        static string ToString(StorageValueTypeDef typeDef) => typeDef.Match(cpt => $"{cpt}", sd => sd.Name);

        public IEnumerable<Variable> Enumerate(IVariableManager manager)
        {
            var storages = GetStorages();

            if (schema is not null)
            {
                foreach (var storageDef in schema.StorageDefs)
                {
                    yield return SchematizedStorageContainer.Create(manager, storageDef, storages);
                }

                var container = new RawStorageContainer(GetStorages);
                yield return new Variable()
                {
                    Name = "#rawStorage",
                    Value = string.Empty,
                    VariablesReference = manager.Add(container),
                };
            }
            else
            {
                foreach (var variable in RawStorageContainer.Enumerate(manager, storages))
                {
                    yield return variable;
                }
            }
        }

        public bool TryEvaluate(ReadOnlyMemory<char> expression, [MaybeNullWhen(false)] out StackItem result, out ReadOnlyMemory<char> remaining)
        {
            result = default;
            remaining = default;
            return false;

        }


        bool TryParseStorageExpression(ReadOnlyMemory<char> expression, ContractStorageSchema schema, out StorageDef storageDef, out ReadOnlyMemory<byte> key, out ReadOnlyMemory<char> remainder)
        {
            key = default;
            remainder = default;
            storageDef = default;

            if (!expression.StartsWith($"{DebugSession.STORAGE_PREFIX}.")) return false;
            expression = expression.Slice(DebugSession.STORAGE_PREFIX.Length + 1);

            for (int i = 0; i < schema.StorageDefs.Count; i++)
            {
                if (expression.StartsWith(schema.StorageDefs[i].Name))
                {
                    storageDef = schema.StorageDefs[i];
                    break;
                }
            }

            if (string.IsNullOrEmpty(storageDef.Name)) return false;
            expression = expression.Slice(storageDef.Name.Length);

            if (storageDef.KeySegments is null) return false;

            var buffer = new ArrayBufferWriter<byte>();
            buffer.Write(storageDef.KeyPrefix.Span);

            foreach (var segment in storageDef.KeySegments)
            {
                if (expression.Span[0] != '[') return false;
                var index = expression.Span.IndexOf(']');
                if (index == -1) return false;
                var segmentValue = expression.Slice(1, index - 1);
                switch (segment.Type)
                {
                    case PrimitiveStorageType.Address: // TODO: address sting 
                    case PrimitiveStorageType.Hash160:
                        {
                            var hash = UInt160.Parse(segmentValue.ToString());
                            buffer.Write(Neo.IO.Helper.ToArray(hash));
                            break;
                        }
                    case PrimitiveStorageType.Hash256:
                        {
                            var hash = UInt256.Parse(segmentValue.ToString());
                            buffer.Write(Neo.IO.Helper.ToArray(hash));
                            break;
                        }
                    case PrimitiveStorageType.Integer:
                        {
                            var value = BigInteger.Parse(segmentValue.Span);
                            buffer.Write(value.ToByteArray());
                            break;

                        }
                    default: throw new NotImplementedException();
                }
                expression = expression.Slice(index + 1);
            }

            key = buffer.WrittenMemory;
            remainder = expression;
            return true;
        }

        static bool TryParseStorageExpression(ReadOnlyMemory<char> expression, out ReadOnlyMemory<byte> key, out ReadOnlyMemory<char> remainder)
        {
            remainder = default;
            key = default;
            if (!expression.StartsWith($"{DebugSession.STORAGE_PREFIX}[")) return false;
            expression = expression.Slice(DebugSession.STORAGE_PREFIX.Length + 1);
            var bracketIndex = expression.Span.IndexOf(']');
            if (bracketIndex == -1) return false;
            remainder = expression.Slice(bracketIndex + 1);
            if (!remainder.StartsWith(".key") && !remainder.StartsWith(".item")) return false;
            expression = expression.Slice(0, bracketIndex);
            if (expression.Span.StartsWith("0x")) expression = expression.Slice(2);
            try
            {
                key = Convert.FromHexString(expression.Span);
                return true;
            }
            catch { return false; }
        }
        public (StackItem? item, ReadOnlyMemory<char> remaining) Evaluate(ReadOnlyMemory<char> expression)
        {
            if (schema is not null)
            {
                if (TryParseStorageExpression(expression, schema, out var storageDef, out var key, out var remainder))
                {
                    if (remainder.Span.StartsWith(".key"))
                    {
                        return (key, remainder.Slice(4));
                    }

                    else if (remainder.Span.StartsWith(".item"))
                    {
                        remainder = remainder.Slice(5);
                        if (TryFindStorageItem(GetStorages(), key, out var item))
                        {

                            return (item.Value, remainder);
                        }
                    }

                }
            }
            {
                if (TryParseStorageExpression(expression, out var key, out var remainder))
                {
                    if (remainder.Span.StartsWith(".key"))
                    {
                        return (key, remainder.Slice(4));
                    }
                    else if (remainder.Span.StartsWith(".item"))
                    {
                        if (TryFindStorageItem(GetStorages(), key, out var item))
                        {
                            return (item.Value, remainder.Slice(5));
                        }
                    }
                }
            }

            throw new InvalidOperationException("Invalid storage evaluation");



            static bool TryFindStorageItem(IEnumerable<(ReadOnlyMemory<byte> key, StorageItem item)> storages, ReadOnlyMemory<byte> key, [MaybeNullWhen(false)] out StorageItem item)
            {
                foreach (var storage in storages)
                {
                    if (key.Span.SequenceEqual(storage.key.Span))
                    {
                        item = storage.item;
                        return true;
                    }
                }

                item = default;
                return false;
            }
        }

        private class RawStorageContainer : IVariableContainer
        {
            Func<IEnumerable<(ReadOnlyMemory<byte> key, StorageItem item)>> getStorages;

            public RawStorageContainer(Func<IEnumerable<(ReadOnlyMemory<byte> key, StorageItem item)>> getStorages)
            {
                this.getStorages = getStorages;
            }

            public static IEnumerable<Variable> Enumerate(IVariableManager manager, IEnumerable<(ReadOnlyMemory<byte> key, StorageItem item)> storages)
            {
                foreach (var (key, item) in storages)
                {
                    var kvp = new KvpContainer(key, item);
                    yield return new Variable()
                    {
                        Name = key.Span.ToHexString(),
                        Value = string.Empty,
                        VariablesReference = manager.Add(kvp),
                        NamedVariables = 2
                    };
                }
            }
            public IEnumerable<Variable> Enumerate(IVariableManager manager)
            {
                return Enumerate(manager, getStorages());
            }
        }

        private class KvpContainer : IVariableContainer
        {
            private readonly ReadOnlyMemory<byte> key;
            private readonly StorageItem item;
            private readonly string prefix;

            public KvpContainer(ReadOnlyMemory<byte> key, StorageItem item)
            {
                this.key = key;
                this.item = item;
                this.prefix = $"{DebugSession.STORAGE_PREFIX}[0x{key.Span.ToHexString()}].";
            }


            Variable CreateVariable(IVariableManager manager, ReadOnlyMemory<byte> buffer, string name)
            {
                if (buffer.Length < 35)
                {
                    return new Variable
                    {
                        Name = name,
                        Value = "0x" + buffer.Span.ToHexString(),
                        Type = $"ByteArray[{buffer.Length}]",
                        EvaluateName = prefix + name,
                    };
                }
                else
                {
                    var valueItem = ByteArrayContainer.Create(manager, buffer, name);
                    valueItem.EvaluateName = prefix + name;
                    return valueItem;
                }
            }

            public IEnumerable<Variable> Enumerate(IVariableManager manager)
            {
                yield return CreateVariable(manager, key, "key");
                yield return CreateVariable(manager, new ReadOnlyMemory<byte>(item.Value), "item");
            }
        }
    }
}
