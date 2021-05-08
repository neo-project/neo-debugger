using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo.SmartContract;
using StackItem = Neo.VM.Types.StackItem;

namespace NeoDebug.Neo3
{
    public class VariableManager : IVariableManager
    {
        private readonly Dictionary<int, IVariableContainer> containers = new();
        private readonly Dictionary<string, (StackItem item, ContractParameterType type)> namedVariables = new();

        public void Clear()
        {
            containers.Clear();
            namedVariables.Clear();
        }

        public bool TryGet(int id, [MaybeNullWhen(false)] out IVariableContainer container)
        {
            return containers.TryGetValue(id, out container);
        }

        public int AddContainer(IVariableContainer container)
        {
            var id = container.GetHashCode();
            if (containers.TryAdd(id, container))
            {
                return id;
            }

            throw new Exception();
        }

        public string AddVariable(string name, StackItem item, ContractParameterType type)
        {
            if (namedVariables.TryAdd(name, (item, type)))
            {
                return name;
            }

            return string.Empty;
        }
    }
}
