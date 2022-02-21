using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo.BlockchainToolkit;

namespace NeoDebug.Neo3
{
    internal class SchematizedStructContainer : IVariableContainer
    {
        readonly StructDef @struct;
        readonly Neo.VM.Types.Array item;
        readonly string evaluatePrefix;

        public SchematizedStructContainer(StructDef @struct, Neo.VM.Types.Array item, string evaluatePrefix)
        {
            if (@struct.Fields.Count != item.Count) throw new ArgumentException();

            this.@struct = @struct;
            this.item = item;
            this.evaluatePrefix = evaluatePrefix;
        }

        public IEnumerable<Variable> Enumerate(IVariableManager manager)
        {
            for (int i = 0; i < @struct.Fields.Count; i++)
            {
                var (name, type) = @struct.Fields[i];
                var value = item[i];
                yield return value.AsVariable(manager, name, type, evaluatePrefix);
            }
        }
    }
}
