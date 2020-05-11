using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using System.Collections.Generic;

namespace NeoDebug.VariableContainers
{
    public interface IVariableContainer
    {
        IEnumerable<Variable> GetVariables();
    }
}
