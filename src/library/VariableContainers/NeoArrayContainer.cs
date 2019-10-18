using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using System.Collections.Generic;

namespace NeoDebug.VariableContainers
{
    internal class NeoArrayContainer : IVariableContainer
    {
        private readonly IVariableContainerSession session;
        private readonly Neo.VM.Types.Array array;

        public NeoArrayContainer(IVariableContainerSession session, Neo.VM.Types.Array array)
        {
            this.session = session;
            this.array = array;
        }

        public static Variable Create(IVariableContainerSession session, Neo.VM.Types.Array array, string name)
        {
            var container = new NeoArrayContainer(session, array);
            var containerID = session.AddVariableContainer(container);
            var typeName = array is Neo.VM.Types.Struct
                ? "Struct" : "Array";
            return new Variable()
            {
                Name = name,
                Type = $"{typeName}[{array.Count}]",
                VariablesReference = containerID,
                IndexedVariables = array.Count,
            };
        }

        public IEnumerable<Variable> GetVariables()
        {
            for (int i = 0; i < array.Count; i++)
            {
                yield return array[i].GetVariable(session, i.ToString();
            }
        }
    }
}
