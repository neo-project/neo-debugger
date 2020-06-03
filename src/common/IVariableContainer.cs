using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using System.Collections.Generic;

namespace NeoDebug
{
    public interface IVariableContainer
    {
        IEnumerable<Variable> Enumerate();
    }
}
