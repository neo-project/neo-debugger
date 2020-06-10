using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo.VM;

namespace NeoDebug.Neo3
{
    class SlotContainer : IVariableContainer
    {
        private readonly IVariableManager manager;
        private readonly Slot? slot;
        private readonly string name;

        public SlotContainer(IVariableManager manager, Slot? slot, string name)
        {
            this.manager = manager;
            this.slot = slot;
            this.name = name;
        }

        public IEnumerable<Variable> Enumerate()
        {
            if (slot != null)
            {
                for (int i = 0; i < slot.Count; i++)
                {
                    var v = slot[i].ToVariable(manager, $"{name}{i}");
                    v.EvaluateName = v.Name;
                    yield return v;
                }
            }
        }
    }
}
