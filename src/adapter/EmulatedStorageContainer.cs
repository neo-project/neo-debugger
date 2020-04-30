using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using NeoDebug.VariableContainers;
using NeoFx;
using System;
using System.Collections.Generic;

namespace NeoDebug
{
    internal class EmulatedStorageContainer : IVariableContainer
    {
        internal class KvpContainer : IVariableContainer
        {
            private readonly IVariableContainerSession session;
            private readonly string hashCode;
            private readonly ReadOnlyMemory<byte> key;
            private readonly ReadOnlyMemory<byte> value;
            private readonly bool constant;

            public KvpContainer(IVariableContainerSession session, int hashCode, ReadOnlyMemory<byte> key, ReadOnlyMemory<byte> value, bool constant)
            {
                this.session = session;
                this.hashCode = hashCode.ToString("x");
                this.key = key;
                this.value = value;
                this.constant = constant;
            }

            public IEnumerable<Variable> GetVariables()
            {
                yield return new Variable()
                {
                    Name = "key",
                    Value = key.Span.ToHexString(),
                    EvaluateName = $"$storage[{hashCode}].key",
                };

                yield return new Variable()
                {
                    Name = "value",
                    Value = value.Span.ToHexString(),
                    EvaluateName = $"$storage[{hashCode}].value",
                };

                yield return new Variable()
                {
                    Name = "constant",
                    Value = constant.ToString(),
                    Type = "Boolean"
                };
            }
        }

        private readonly UInt160 scriptHash;
        private readonly IVariableContainerSession session;
        private readonly EmulatedStorage storage;

        public EmulatedStorageContainer(IVariableContainerSession session, UInt160 scriptHash, EmulatedStorage storage)
        {
            this.session = session;
            this.scriptHash = scriptHash;
            this.storage = storage;
        }

        public IEnumerable<Variable> GetVariables()
        {
            foreach (var (key, item) in storage.EnumerateStorage(scriptHash))
            {
                var keyHashCode = key.Span.GetSequenceHashCode();
                yield return new Variable()
                {
                    Name = keyHashCode.ToString("x"),
                    Value = string.Empty,
                    VariablesReference = session.AddVariableContainer(
                        new KvpContainer(session, keyHashCode, key, item.Value, item.IsConstant)),
                    NamedVariables = 3
                };
            }
        }


        // private bool TryGetStorageKey(string keyString, out StorageKey storageKey)
        // {
        //     if (BigInteger.TryParse(keyString.AsSpan().Slice(2), NumberStyles.HexNumber, null, out var keyBigInt))
        //     {
        //         var keyBuffer = new byte[keyBigInt.GetByteCount()];
        //         if (keyBigInt.TryWriteBytes(keyBuffer, out var keyWritten)
        //             && keyWritten == keyBuffer.Length)
        //         {
        //             storageKey = new StorageKey(scriptHash, keyBuffer);
        //             return true;
        //         }
        //     }

        //     storageKey = default;
        //     return false;
        // }

        // public EvaluateResponse Evaluate(EvaluateArguments args)
        // {
        //     var (typeHint, index, variableName) = DebugAdapter.ParseEvalExpression(args.Expression);
        //     var match = storageRegex.Match(variableName);

        //     //if (!index.HasValue && match.Success
        //     //    && TryGetStorageKey(match.Groups[1].Value, out var storageKey))
        //     //{
        //     //}
        //     //return DebugAdapter.FailedEvaluation;

        //     return new EvaluateResponse()
        //     {
        //         Result = variableName
        //     };
        //     //    var variableType = match.Groups[2].Value;

        //     //    if (variableType == key)
        //     //    {

        //     //    }
        //     //        && storage.TryGetStorage(new StorageKey(scriptHash, keyBuffer), out var item)
        //     //        )
        //     //    {
        //     //        Stora
        //     //        var keyHash =- 
        //     //    }
        //     //        key
        //     //    if ()
        //     //    {
        //     //        return DebugAdapter.FailedEvaluation;
        //     //    }




        //     //    if (variableType == "key")
        //     //    {
        //     //        var variable = ByteArrayContainer.Create(session, bigInteger.ToByteArray(), null);

        //     //    }
        //     //        var key = new StorageKey(scriptHash, bigInteger.ToByteArray());





        //     //    if (storage.TryGetStorage(key, out var item))
        //     //    {
        //     //        switch (match.Groups[2].Value)
        //     //        {
        //     //            case "key":
        //     //                {
        //     //                    var keyVariable = ByteArrayContainer.Create(session, key.ToArray(), "key");

        //     //                }
        //     //                item
        //     //                break;
        //     //            case "value":
        //     //                break;
        //     //        }
        //     //    }
        //     //}

        // }
    }
}
