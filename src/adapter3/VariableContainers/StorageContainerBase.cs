﻿using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo;
using Neo.BlockchainToolkit.Models;
using Neo.Cryptography.ECC;
using Neo.IO;
using Neo.SmartContract;

namespace NeoDebug.Neo3
{
    using StackItem = Neo.VM.Types.StackItem;

    internal abstract class StorageContainerBase : IVariableContainer
    {
        readonly IReadOnlyList<StorageDef> storageDefs;
        readonly byte addressVersion;
        readonly StorageView storageView;

        protected StorageContainerBase(IReadOnlyList<StorageDef> storageDefs, byte addressVersion, StorageView storageView)
        {
            this.storageDefs = storageDefs;
            this.addressVersion = addressVersion;
            this.storageView = storageView;
        }

        protected abstract IEnumerable<(ReadOnlyMemory<byte> key, StorageItem item)> GetStorages();

        public IEnumerable<Variable> Enumerate(IVariableManager manager)
        {
            if (storageDefs.Count > 0)
            {
                var storages = GetStorages().ToArray();
                return storageDefs
                    .Select(storageDef => CreateStorageVariable(manager, storageDef, storages))
                    .Append(new Variable()
                    {
                        Name = "#rawStorage",
                        Value = string.Empty,
                        VariablesReference = manager.Add(new RawStorageContainer(storages, storageView)),
                    });
            }
            else
            {
                return EnumerateRawStorage(manager, GetStorages(), storageView);
            }
        }

        public bool TryEvaluate(ReadOnlyMemory<char> expression, [MaybeNullWhen(false)] out ExpressionEvalContext context)
        {
            if (expression.StartsWith(DebugSession.STORAGE_PREFIX))
            {
                expression = expression.Slice(DebugSession.STORAGE_PREFIX.Length);
                if (expression.IsEmpty)
                {
                    throw new InvalidOperationException("Empty storage expression");
                }
                else
                {
                    if (expression.Span[0] == '.')
                    {
                        expression = expression.Slice(1);
                        for (int i = 0; i < storageDefs.Count; i++)
                        {
                            if (expression.StartsWith(storageDefs[i].Name))
                            {
                                return TryEvaluateSchemaStorage(
                                    expression.Slice(storageDefs[i].Name.Length),
                                    storageDefs[i],
                                    out context);
                            }
                        }
                        throw new InvalidOperationException($"Unknown StorageDef in storage expression {new String(expression.Span)}");
                    }
                    else if (expression.Span[0] == '[')
                    {
                        expression = expression.Slice(1);
                        var bracketIndex = expression.Span.IndexOf(']');
                        if (bracketIndex == -1) throw new InvalidOperationException($"Missing end bracket in storage expression {new String(expression.Span)}");
                        return TryEvaluateRawStorage(
                            expression.Slice(bracketIndex + 1),
                            expression.Slice(0, bracketIndex),
                            out context);

                    }
                    else
                    {
                        throw new InvalidOperationException($"Invalid storage expression {new String(expression.Span)}");
                    }
                }
            }

            context = default;
            return false;
        }

        bool TryEvaluateSchemaStorage(ReadOnlyMemory<char> expression, StorageDef storageDef, [MaybeNullWhen(false)] out ExpressionEvalContext context)
        {
            var keyWriter = new ArrayBufferWriter<byte>();
            keyWriter.Write(storageDef.KeyPrefix.Span);
            foreach (var segment in storageDef.KeySegments)
            {
                if (expression.IsEmpty || expression.Span[0] != '[') throw new InvalidOperationException($"Invalid storage expression {new string(expression.Span)}");
                expression = expression.Slice(1);
                var bracketIndex = expression.Span.IndexOf(']');
                if (bracketIndex == -1) throw new InvalidOperationException($"Invalid storage expression {new string(expression.Span)}");
                keyWriter.Write(ParsePrimitiveType(expression.Slice(0, bracketIndex), segment.Type).Span);
                expression = expression.Slice(bracketIndex + 1);
            }

            if (expression.StartsWith(".key"))
            {
                var result = new Neo.VM.Types.ByteString(keyWriter.WrittenMemory);
                var resultType = new PrimitiveContractType(PrimitiveType.ByteArray);
                var remaining = expression.Slice(4);
                context = new ExpressionEvalContext(remaining, result, resultType);
                return true;
            }

            if (expression.StartsWith(".item"))
            {
                var (_, item) = GetStorages().SingleOrDefault(kvp => kvp.key.Span.SequenceEqual(keyWriter.WrittenSpan));
                var remaining = expression.Slice(5);

                if (item is null)
                {
                    var result = Neo.VM.Types.StackItem.Null;
                    var resultType = storageDef.ValueType;
                    context = new ExpressionEvalContext(remaining, result, resultType);
                    return true;
                }
                else
                {
                    var result = AsStackItem(item, storageDef.ValueType);
                    var resultType = storageDef.ValueType;
                    context = new ExpressionEvalContext(remaining, result, resultType);
                    return true;
                }
            }

            context = default;
            return false;
        }

        bool TryEvaluateRawStorage(ReadOnlyMemory<char> expression, ReadOnlyMemory<char> keyBuffer, [MaybeNullWhen(false)] out ExpressionEvalContext context)
        {
            return storageView switch
            {
                StorageView.HashedKey => TryEvaluateRawStorageHashedKey(expression, keyBuffer, out context),
                StorageView.FullKey => TryEvaluateRawStorageFullKey(expression, keyBuffer, out context),
                _ => throw new Exception()
            };
        }

        bool TryEvaluateRawStorageFullKey(ReadOnlyMemory<char> expression, ReadOnlyMemory<char> keyBuffer, [MaybeNullWhen(false)] out ExpressionEvalContext context)
        {
            if (keyBuffer.StartsWith("0x")) { keyBuffer = keyBuffer.Slice(2); }
            var key = Convert.FromHexString(keyBuffer.Span);

            if (expression.StartsWith(".key"))
            {
                var result = new Neo.VM.Types.ByteString(key);
                var remaining = expression.Slice(4);
                context = new ExpressionEvalContext(remaining, result, null);
                return true;
            }

            if (expression.StartsWith(".item"))
            {
                var storage = GetStorages().SingleOrDefault(kvp => kvp.key.Span.SequenceEqual(key));
                var result = storage.item is null
                    ? Neo.VM.Types.Null.Null
                    : new Neo.VM.Types.ByteString(storage.item.Value);
                var remaining = expression.Slice(5);
                context = new ExpressionEvalContext(remaining, result, null);
                return true;
            }

            context = default;
            return false;
        }

        bool TryEvaluateRawStorageHashedKey(ReadOnlyMemory<char> expression, ReadOnlyMemory<char> keyBuffer, [MaybeNullWhen(false)] out ExpressionEvalContext context)
        {
            if (int.TryParse(keyBuffer.Span, System.Globalization.NumberStyles.HexNumber, null, out var keyHash))
            {
                foreach (var (key, item) in GetStorages())
                {
                    var storageKeyHash = GetSequenceHashCode(key.Span);
                    if (storageKeyHash == keyHash)
                    {
                        if (expression.StartsWith(".key"))
                        {
                            var result = new Neo.VM.Types.ByteString(key);
                            var remaining = expression.Slice(4);
                            context = new ExpressionEvalContext(remaining, result, null);
                            return true;
                        }

                        if (expression.StartsWith(".item"))
                        {

                            var result = item is null
                                ? Neo.VM.Types.Null.Null
                                : new Neo.VM.Types.ByteString(item.Value);
                            var remaining = expression.Slice(5);
                            context = new ExpressionEvalContext(remaining, result, null);
                            return true;
                        }
                    }
                }
            }

            context = default;
            return false;
        }


        ReadOnlyMemory<byte> ParsePrimitiveType(ReadOnlyMemory<char> buffer, PrimitiveType type)
        {
            return type switch
            {
                PrimitiveType.Address => buffer.FromAddress(addressVersion).ToArray(),
                PrimitiveType.Boolean => (bool.Parse(buffer.Span) ? BigInteger.One : BigInteger.Zero).ToByteArray(),
                PrimitiveType.ByteArray => buffer.FromHexString(),
                PrimitiveType.Hash160 => UInt160.Parse(new string(buffer.Span)).ToArray(),
                PrimitiveType.Hash256 => UInt256.Parse(new string(buffer.Span)).ToArray(),
                PrimitiveType.Integer => BigInteger.Parse(buffer.Span).ToByteArray(),
                PrimitiveType.PublicKey => ECPoint.Parse(new string(buffer.Span), ECCurve.Secp256r1).ToArray(),
                PrimitiveType.Signature => buffer.FromHexString(),
                PrimitiveType.String => Neo.Utility.StrictUTF8.GetBytes(new string(buffer.Span)),
                _ => throw new InvalidOperationException($"{type}"),
            };
        }

        static StackItem AsStackItem(StorageItem item, ContractType type)
        {
            return type switch
            {
                PrimitiveContractType primitive => primitive.Type switch
                {
                    PrimitiveType.Boolean => new Neo.VM.Types.Boolean(new BigInteger(item.Value) != BigInteger.Zero),
                    PrimitiveType.Integer => new Neo.VM.Types.Integer(new BigInteger(item.Value)),
                    _ => new Neo.VM.Types.ByteString(item.Value),
                },
                StructContractType _ => (Neo.VM.Types.Array)BinarySerializer.Deserialize(item.Value, Neo.VM.ExecutionEngineLimits.Default),
                // MapContractType _ => (Neo.VM.Types.Map)BinarySerializer.Deserialize(item.Value, Neo.VM.ExecutionEngineLimits.Default),
                _ => throw new InvalidOperationException($"Unknown ContractType {type.GetType().Name}"),
            };
        }

        Variable CreateStorageVariable(IVariableManager manager, StorageDef storageDef, IEnumerable<(ReadOnlyMemory<byte> key, StorageItem item)> storages)
        {
            var evalName = $"{DebugSession.STORAGE_PREFIX}.{storageDef.Name}";
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
                        EvaluateName = evalName + ".item",
                    };
                }
                else
                {
                    var variable = ToVariable(item, manager, storageDef, addressVersion);
                    variable.Name = storageDef.Name;
                    variable.Type = storageDef.ValueType.AsTypeName();
                    variable.EvaluateName = evalName + ".item";
                    return variable;
                }
            }
            else
            {
                var storageItems = storages.Where(s => s.key.Span.StartsWith(storageDef.KeyPrefix.Span)).ToArray();
                var container = new SchematizedStorageContainer(storageDef, addressVersion, evalName, storageItems);
                var keyType = string.Join(", ", storageDef.KeySegments.Select(s => $"{s.Name}: {s.Type}"));
                return new Variable()
                {
                    Name = storageDef.Name,
                    Type = $"({keyType}) -> {storageDef.ValueType.AsTypeName()}[{storageItems.Length}]",
                    Value = string.Empty,
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

        static IEnumerable<Variable> EnumerateRawStorage(IVariableManager manager, IEnumerable<(ReadOnlyMemory<byte> key, StorageItem item)> storages, StorageView storageView)
        {
            var hashedKeyView = storageView switch { 
                StorageView.HashedKey => true,
                StorageView.FullKey => false,
                _ => throw new Exception($"Invalid StorageView {storageView}")
            };

            foreach (var (key, item) in storages)
            {
                var keyHashCode = hashedKeyView ? GetSequenceHashCode(key.Span).ToString("x8")  : string.Empty;
                IVariableContainer container = hashedKeyView 
                    ? new HashedKeyItemContainer(key, item, keyHashCode) 
                    : new FullKeyItemContainer(key, item);

                yield return new Variable()
                {
                    Name = hashedKeyView ? keyHashCode : key.Span.ToHexString(),
                    Value = string.Empty,
                    VariablesReference = manager.Add(container),
                    NamedVariables = 2
                };
            }
        }

        static int GetSequenceHashCode(ReadOnlySpan<byte> span)
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

        class RawStorageContainer : IVariableContainer
        {
            readonly IEnumerable<(ReadOnlyMemory<byte> key, StorageItem item)> storages;
            readonly StorageView storageView;

            public RawStorageContainer(IReadOnlyList<(ReadOnlyMemory<byte> key, StorageItem item)> storages, StorageView storageView)
            {
                this.storages = storages;
                this.storageView = storageView;
            }

            public IEnumerable<Variable> Enumerate(IVariableManager manager)
            {
                return EnumerateRawStorage(manager, storages, storageView);
            }
        }

        class HashedKeyItemContainer : IVariableContainer
        {
            private readonly ReadOnlyMemory<byte> key;
            private readonly StorageItem item;
            private readonly string prefix;

            public HashedKeyItemContainer(ReadOnlyMemory<byte> key, StorageItem item, string hashCode)
            {
                this.key = key;
                this.item = item;
                this.prefix = $"{DebugSession.STORAGE_PREFIX}[{hashCode}].";
            }

            public IEnumerable<Variable> Enumerate(IVariableManager manager)
            {
                var keyItem = ByteArrayContainer.Create(manager, key, "key");
                keyItem.EvaluateName = prefix + "key";
                yield return keyItem;

                var valueItem = ByteArrayContainer.Create(manager, new ReadOnlyMemory<byte>(item.Value), "item");
                valueItem.EvaluateName = prefix + "item";
                yield return valueItem;
            }
        }

        class FullKeyItemContainer : IVariableContainer
        {
            private readonly ReadOnlyMemory<byte> key;
            private readonly StorageItem item;
            private readonly string prefix;

            public FullKeyItemContainer(ReadOnlyMemory<byte> key, StorageItem item)
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
                variable.EvaluateName = prefix + name;
                return variable;
            }
        }

        class SchematizedStorageContainer : IVariableContainer
        {
            readonly StorageDef storageDef;
            readonly byte addressVersion;
            readonly string evalPrefix;
            readonly IReadOnlyList<(ReadOnlyMemory<byte> key, StorageItem item)> storages;

            public SchematizedStorageContainer(StorageDef storageDef, byte addressVersion, string evalPrefix, IReadOnlyList<(ReadOnlyMemory<byte> key, StorageItem item)> storages)
            {
                this.storageDef = storageDef;
                this.addressVersion = addressVersion;
                this.evalPrefix = evalPrefix;
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
                        variable.EvaluateName = $"{evalPrefix}[{variable.Name}].item";
                        yield return variable;
                    }
                    else
                    {
                        var container = new KeyItemContainer(segments, item, storageDef, evalPrefix, addressVersion);
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
            readonly string evalPrefix;
            readonly byte addressVersion;

            public KeyItemContainer(IReadOnlyList<KeySegment> keySegments, StorageItem storageItem, StorageDef storageDef, string evalPrefix, byte addressVersion)
            {
                this.keySegments = keySegments;
                this.storageItem = storageItem;
                this.storageDef = storageDef;
                this.evalPrefix = evalPrefix;
                this.addressVersion = addressVersion;
            }

            public IEnumerable<Variable> Enumerate(IVariableManager manager)
            {
                var builder = new System.Text.StringBuilder(evalPrefix);
                for (int i = 0; i < keySegments.Count; i++)
                {
                    builder.Append($"[{keySegments[i].AsString(addressVersion)}]");
                }
                var evalName = builder.ToString();

                var keyContainer = new KeySegmentsContainer(keySegments, addressVersion);
                yield return new Variable()
                {
                    Name = "key",
                    Value = " ",
                    Type = string.Join(", ", storageDef.KeySegments.Select(s => $"{s.Name}: {s.Type}")),
                    VariablesReference = manager.Add(keyContainer),
                    NamedVariables = keySegments.Count,
                    EvaluateName = evalName + ".key",
                };

                var variable = ToVariable(storageItem, manager, storageDef, addressVersion);
                variable.Name = "item";
                variable.EvaluateName = evalName + ".item";
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
