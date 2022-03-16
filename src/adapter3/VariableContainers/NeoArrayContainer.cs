using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo.BlockchainToolkit.Models;
using OneOf;
using None = OneOf.Types.None;

namespace NeoDebug.Neo3
{
    using NeoArray = Neo.VM.Types.Array;
    using NeoStruct = Neo.VM.Types.Struct;

    class NeoArrayContainer : IVariableContainer
    {
        readonly string name;
        readonly NeoArray array;
        readonly OneOf<StructContractType, ArrayContractType, None> type;
        readonly byte addressVersion;

        public NeoArrayContainer(string name, NeoArray array, OneOf<StructContractType, ArrayContractType, None> type, byte addressVersion)
        {
            if (type.TryPickT0(out var structType, out _)
                && structType.Fields.Count != array.Count)
            {
                throw new ArgumentException($"Expected {structType.Fields.Count} array fields, received {array.Count}");
            }

            this.name = name;
            this.array = array;
            this.type = type;
            this.addressVersion = addressVersion;
        }

        public static Variable Create(IVariableManager manager, NeoArray array, string name, StructContractType type, byte addressVersion)
        {
            var container = new NeoArrayContainer(name, array, type, addressVersion);
            return new Variable()
            {
                Name = name,
                Value = type.ShortName,
                VariablesReference = manager.Add(container),
                NamedVariables = array.Count,
            };
        }

        public static Variable Create(IVariableManager manager, NeoArray array, string name, ArrayContractType type, byte addressVersion)
        {
            var container = new NeoArrayContainer(name, array, type, addressVersion);
            return new Variable()
            {
                Name = name,
                Value = $"Array<{type.Type.AsTypeName()}>[{array.Count}]",
                VariablesReference = manager.Add(container),
                IndexedVariables = array.Count,
            };
        }

        public static Variable Create(IVariableManager manager, NeoArray array, string name)
        {
            var container = new NeoArrayContainer(name, array, default(None), 0);
            return new Variable()
            {
                Name = name,
                Value = $"{(array is NeoStruct ? "Struct" : "Array<>")}[{array.Count}]",
                VariablesReference = manager.Add(container),
                IndexedVariables = array.Count,
            };
        }

        public IEnumerable<Variable> Enumerate(IVariableManager manager)
        {
            for (int i = 0; i < array.Count; i++)
            {
                var fieldValue = array[i];
                var fieldType = type.Match<ContractType>(
                    s => s.Fields[i].Type,
                    a => a.Type,
                    _ => ContractType.Unspecified);

                string fieldName, evalName;
                if (type.TryPickT0(out var @struct, out _))
                {
                    fieldName = @struct.Fields[i].Name;
                    evalName = $"{name}.{fieldName}";
                }
                else
                {
                    fieldName = $"{i}";
                    evalName = $"{name}[{fieldName}]";
                }

                var variable = fieldValue.AsVariable(manager, fieldName, fieldType, addressVersion);
                variable.EvaluateName = evalName;
                yield return variable;
            }
        }
    }
}
