using System.Collections.Generic;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo.SmartContract;

namespace NeoDebug.Neo3
{
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
}
