using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo.VM;
using NeoDebug.VariableContainers;
using NeoFx.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace NeoDebug.Adapter.ModelAdapters
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
