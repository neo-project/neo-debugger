using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace NeoDebug.Neo3
{
    public class VariableManager : IVariableManager
    {
        private readonly Dictionary<int, IVariableContainer> containers = new();

        public void Clear()
        {
            containers.Clear();
        }

        public bool TryGet(int id, [MaybeNullWhen(false)] out IVariableContainer container)
        {
            return containers.TryGetValue(id, out container);
        }

        public int Add(IVariableContainer container)
        {
            var id = container.GetHashCode();
            if (!containers.TryAdd(id, container))
            {
                throw new Exception("specified container already exists");
            }
            return id;
        }
    }
}
