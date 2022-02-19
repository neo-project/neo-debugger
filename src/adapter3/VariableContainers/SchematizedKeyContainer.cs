using System.Collections.Generic;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo.BlockchainToolkit;
using Neo.SmartContract;

namespace NeoDebug.Neo3
{
    internal class SchematizedKeyContainer : IVariableContainer
    {
        readonly IReadOnlyList<(string name, PrimitiveStorageType type, object value)> segments;

        public SchematizedKeyContainer(IReadOnlyList<(string name, PrimitiveStorageType type, object value)> segments)
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
                    Value = segment.AsString(), // plumb address version
                    Type = $"{segment.type}"
                };
            }
        }
    }
}
