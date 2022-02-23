using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo.BlockchainToolkit.Models;

namespace NeoDebug.Neo3
{
    using NeoArray = Neo.VM.Types.Array;
    using NeoStruct = Neo.VM.Types.Struct;

    class NeoArrayContainer : IVariableContainer
    {
        readonly NeoArray array;
        readonly StructContractType? type;
        readonly byte addressVersion;

        public NeoArrayContainer(NeoArray array)
        {
            this.array = array;
            this.type = null;
        }

        public NeoArrayContainer(NeoArray array, StructContractType type, byte addressVersion)
        {
            if (type is not null && type.Fields.Count != array.Count)
            {
                throw new ArgumentException($"Expected {type.Fields.Count} array fields, received {array.Count}");
            }

            this.array = array;
            this.type = type;
            this.addressVersion = addressVersion;
        }


        public static Variable Create(IVariableManager manager, NeoArray array, string name, StructContractType type, byte addressVersion)
        {
            var container = new NeoArrayContainer(array, type, addressVersion);
            return new Variable()
            {
                Name = name,
                Value = $"{type.AsTypeName()}[{array.Count}]",
                VariablesReference = manager.Add(container),
                NamedVariables = array.Count,
            };
        }

        public static Variable Create(IVariableManager manager, NeoArray array, string name)
        {
            var typeName = array is NeoStruct ? "Struct" : "Array";
            var container = new NeoArrayContainer(array);
            return new Variable()
            {
                Name = name,
                Value = $"{typeName}[{array.Count}]",
                VariablesReference = manager.Add(container),
                IndexedVariables = array.Count,
            };
        }

        public IEnumerable<Variable> Enumerate(IVariableManager manager)
        {
            for (int i = 0; i < array.Count; i++)
            {
                var fieldValue = array[i];
                var variable = type is null
                    ? fieldValue.AsVariable(manager, $"{i}")
                    : fieldValue.AsVariable(manager, type.Fields[i].Name, type.Fields[i].Type, addressVersion);
                yield return variable;
            }
        }
    }
}
