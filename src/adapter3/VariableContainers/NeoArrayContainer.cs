using System.Collections.Generic;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;

namespace NeoDebug.Neo3
{
    using NeoArray = Neo.VM.Types.Array;
    using NeoStruct = Neo.VM.Types.Struct;

    class NeoArrayContainer : IVariableContainer
    {
        private readonly NeoArray array;

        public NeoArrayContainer(NeoArray array)
        {
            this.array = array;
        }

        public static Variable Create(IVariableManager manager, NeoArray array, string name)
        {
            var typeName = array is NeoStruct ? "Struct" : "Array";
            var container = new NeoArrayContainer(array);
            return new Variable()
            {
                Name = name,
                Value = $"{typeName}[{array.Count}]",
                VariablesReference = manager.Add(container),
                IndexedVariables = array.Count,
            };
        }

        public IEnumerable<Variable> Enumerate(IVariableManager manager)
        {
            for (int i = 0; i < array.Count; i++)
            {
                yield return array[i].ToVariable(manager, $"{i}");
            }
        }
    }
}
