using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo;
using Neo.BlockchainToolkit.Models;
using OneOf;
using None = OneOf.Types.None;

namespace NeoDebug.Neo3
{
    using NeoMap = Neo.VM.Types.Map;

    class NeoMapContainer : IVariableContainer
    {
        readonly NeoMap map;
        readonly OneOf<MapContractType, None> type;
        readonly byte addressVersion;

        public NeoMapContainer(NeoMap map, OneOf<MapContractType, None> type, byte addressVersion)
        {
            this.map = map;
            this.type = type;
            this.addressVersion = addressVersion;
        }

        public static Variable Create(IVariableManager manager, NeoMap map, string name, MapContractType type, byte addressVersion)
        {
            var container = new NeoMapContainer(map, type, addressVersion);
            return new Variable()
            {
                Name = name,
                Value = $"Map<{type.KeyType},{type.ValueType.AsTypeName()}>[{map.Count}]",
                VariablesReference = manager.Add(container),
                NamedVariables = map.Count
            };
        }

        public static Variable Create(IVariableManager manager, NeoMap map, string name)
        {
            var container = new NeoMapContainer(map, default(None), 0);
            return new Variable()
            {
                Name = name,
                Value = $"Map<>[{map.Count}]",
                VariablesReference = manager.Add(container),
                NamedVariables = map.Count
            };
        }

        public IEnumerable<Variable> Enumerate(IVariableManager manager)
        {
            foreach (var key in map.Keys)
            {
                var keyString = key switch
                {
                    Neo.VM.Types.Boolean @bool => @bool.GetBoolean().ToString(),
                    Neo.VM.Types.ByteString byteString => byteString.GetSpan().ToHexString(),
                    Neo.VM.Types.Integer @int => @int.GetInteger().ToString(),
                    _ => throw new NotImplementedException($"Unknown primitive type {key.GetType()}"),
                };

                var valueType = type.Match<ContractType>(
                    m => m.ValueType,
                    _ => ContractType.Unspecified);

                yield return map[key].AsVariable(manager, keyString, valueType, addressVersion);
            }
        }
    }
}
