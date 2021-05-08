using Neo.SmartContract;

namespace NeoDebug.Neo3
{
    public interface IVariableManager
    {
        int AddContainer(IVariableContainer container);
        string AddVariable(string name, Neo.VM.Types.StackItem item, ContractParameterType type);
    }
}
