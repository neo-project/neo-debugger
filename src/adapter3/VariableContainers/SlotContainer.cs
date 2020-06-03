using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo.VM;

namespace NeoDebug.Neo3
{
    class SlotContainer : IVariableContainer
    {
        private readonly IVariableManager manager;
        private readonly Slot slot;

        public SlotContainer(IVariableManager manager, Slot slot)
        {
            this.manager = manager;
            this.slot = slot;
        }

        public IEnumerable<Variable> Enumerate()
        {
            for (int i = 0; i < slot.Count; i++)
            {
                yield return slot[i].ToVariable(manager, $"variable {i}");
            }
        }
    }
}
