using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using NeoDebug.VariableContainers;
using NeoFx;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace NeoDebug.Adapter
{
    internal class EmulatedStorageContainer : IVariableContainer
    {
        internal class KvpContainer : IVariableContainer
        {
            private readonly IVariableContainerSession session;
            private readonly ReadOnlyMemory<byte> key;
            private readonly ReadOnlyMemory<byte> value;
            private readonly bool constant;

            public KvpContainer(IVariableContainerSession session, ReadOnlyMemory<byte> key, ReadOnlyMemory<byte> value, bool constant)
            {
                this.session = session;
                this.key = key;
                this.value = value;
                this.constant = constant;
            }

            public IEnumerable<Variable> GetVariables()
            {
                // TODO remove .ToArray() calls
                yield return ByteArrayContainer.Create(session, key.ToArray(), "key");
                yield return ByteArrayContainer.Create(session, value.ToArray(), "value");
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
                yield return new Variable()
                {
                    Name = "0x" + new BigInteger(key.Span).ToString("x"),
                    VariablesReference = session.AddVariableContainer(
                        new KvpContainer(session, key, item.Value, item.IsConstant)),
                    NamedVariables = 3
                };
            }
        }

        // static readonly Regex storageRegex = new Regex(@"^\$storage\[(\d+)\]\.(key|value)$");

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
