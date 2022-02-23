using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo;
using Neo.BlockchainToolkit.Models;

namespace NeoDebug.Neo3
{
    using NeoMap = Neo.VM.Types.Map;

    class NeoMapContainer : IVariableContainer
    {
        readonly NeoMap map;
        readonly MapContractType? type;

        public NeoMapContainer(NeoMap map, MapContractType? type = null)
        {
            this.map = map;
            this.type = type;
        }

        public static Variable Create(IVariableManager manager, NeoMap map, string name, MapContractType? type = null)
        {
            var container = new NeoMapContainer(map, type);
            return new Variable()
            {
                Name = name,
                Value = $"Map[{map.Count}]",
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

                yield return map[key].ToVariable(manager, keyString);
            }
        }
    }
}
