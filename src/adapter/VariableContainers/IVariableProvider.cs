using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;

namespace NeoDebug.VariableContainers
{
    public interface IVariableProvider
    {
        Variable GetVariable(IVariableContainerSession session, string name);
    }
}
