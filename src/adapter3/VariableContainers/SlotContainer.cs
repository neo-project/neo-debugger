using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo.VM;

namespace NeoDebug.Neo3
{
    class SlotContainer : IVariableContainer
    {
        private readonly Slot? slot;
        private readonly string prefix;

        public SlotContainer(string prefix, Slot? slot)
        {
            this.slot = slot;
            this.prefix = prefix;
        }

        public IEnumerable<Variable> Enumerate(IVariableManager manager)
        {
            if (slot != null)
            {
                for (int i = 0; i < slot.Count; i++)
                {
                    var v = slot[i].ToVariable(manager, $"{prefix}{i}");
                    yield return v.ForEvaluation("#");
                }
            }
        }
    }
}
