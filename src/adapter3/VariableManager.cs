using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace NeoDebug.Neo3
{
    public class VariableManager : IVariableManager
    {
        private readonly Dictionary<int, IVariableContainer> containers = new Dictionary<int, IVariableContainer>();

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
            if (containers.TryAdd(id, container))
            {
                return id;
            }

            throw new Exception();
        }
    }
}
