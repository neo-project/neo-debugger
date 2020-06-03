using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;

namespace NeoDebug
{
    public interface IVariableProvider
    {
        Variable GetVariable(IVariableManager session, string name);
    }
}
