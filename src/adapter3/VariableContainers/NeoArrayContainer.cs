using System.Collections.Generic;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;

namespace NeoDebug.Neo3
{
    using NeoArray = Neo.VM.Types.Array;
    using NeoStruct = Neo.VM.Types.Struct;

    class NeoArrayContainer : IVariableContainer
    {
        private readonly IVariableManager manager;
        private readonly NeoArray array;

        public NeoArrayContainer(IVariableManager manager, NeoArray array)
        {
            this.manager = manager;
            this.array = array;
        }

        public static Variable Create(IVariableManager manager, NeoArray array, string name)
        {
            var container = new NeoArrayContainer(manager, array);
            var containerID = manager.Add(container);
            return new Variable()
            {
                Name = name,
                Type = $"{(array is NeoStruct ? "Struct" : "Array")}[{array.Count}]",
                Value = string.Empty,
                VariablesReference = containerID,
                IndexedVariables = array.Count,
            };
        }

        public IEnumerable<Variable> Enumerate()
        {
            for (int i = 0; i < array.Count; i++)
            {
                yield return array[i].ToVariable(manager, $"{i}");
            }
        }
    }
}
