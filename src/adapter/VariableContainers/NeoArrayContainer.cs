using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using System.Collections.Generic;

namespace NeoDebug.VariableContainers
{
    class NeoArrayContainer : IVariableContainer
    {
        private readonly IVariableContainerSession session;
        private readonly Neo.VM.Types.Array array;
        private readonly string? name;

        private NeoArrayContainer(IVariableContainerSession session, Neo.VM.Types.Array array, string name)
        {
            this.session = session;
            this.array = array;
            this.name = name;
        }

        public static Variable Create(IVariableContainerSession session, Neo.VM.Types.Array array, string name)
        {
            var container = new NeoArrayContainer(session, array, name);
            var containerID = session.AddVariableContainer(container);
            var typeName = array is Neo.VM.Types.Struct
                ? "Struct" : "Array";
            return new Variable()
            {
                Name = name,
                Type = $"{typeName}[{array.Count}]",
                Value = string.Empty,
                VariablesReference = containerID,
                IndexedVariables = array.Count,
            };
        }

        public IEnumerable<Variable> GetVariables()
        {
            for (int i = 0; i < array.Count; i++)
            {
                var variable = array[i].GetVariable(session, i.ToString());
                variable.EvaluateName = $"{name}[{i}]";
                variable.Value = variable.Type;
                yield return variable;
            }
        }
    }
}
