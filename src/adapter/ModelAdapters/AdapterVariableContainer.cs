using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using NeoDebug.VariableContainers;
using System.Collections.Generic;



namespace NeoDebug.ModelAdapters
{
    class AdapterVariableContainer : IVariableContainer
    {
        private readonly IVariableContainer adapter;

        public AdapterVariableContainer(IVariableContainer adapter)
        {
            this.adapter = adapter;
        }

        public IEnumerable<Variable> GetVariables()
        {
            return adapter.GetVariables();
        }
    }
}
