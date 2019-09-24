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

        public IEnumerable<Variable> GetVariables()
        {
            for (int i = 0; i < array.Count; i++)
            {
                var variable = array[i].GetVariable(session);
                variable.Name = i.ToString();
                yield return variable;
            }
        }
    }
}
