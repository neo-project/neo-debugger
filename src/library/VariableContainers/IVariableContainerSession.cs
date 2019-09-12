using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using System;
using System.Collections.Generic;
using System.Text;

namespace NeoDebug.VariableContainers
{
    public interface IVariableContainerSession
    {
        int AddVariableContainer(IVariableContainer container);
    }
}
