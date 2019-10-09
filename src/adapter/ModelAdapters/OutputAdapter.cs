using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo.VM;
using NeoDebug.VariableContainers;
using NeoFx.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace NeoDebug.Adapter.ModelAdapters
{
    internal class OutputAdatper : AdapterBase, IVariableProvider, IVariableContainer
    {
        public readonly TransactionOutput Output;

        public OutputAdatper(TransactionOutput output)
        {
            Output = output;
        }

        public bool GetAssetId(ExecutionEngine engine)
        {
            if (Output.AssetId.TryToArray(out var array))
            {
                engine.CurrentContext.EvaluationStack.Push(array);
                return true;
            }

            return false;
        }

        public bool GetValue(ExecutionEngine engine)
        {
            engine.CurrentContext.EvaluationStack.Push(Output.Value);
            return true;
        }

        public bool GetScriptHash(ExecutionEngine engine)
        {
            if (Output.ScriptHash.TryToArray(out var array))
            {
                engine.CurrentContext.EvaluationStack.Push(array);
                return true;
            }

            return false;
        }


        public Variable GetVariable(IVariableContainerSession session)
        {
            return new Variable()
            {
                Name = "Output",
                Type = "TransactionOutput",
                VariablesReference = session.AddVariableContainer(new AdapterVariableContainer(this)),
                NamedVariables = 3,
            };
        }

        public IEnumerable<Variable> GetVariables()
        {
            yield return new Variable()
            {
                Name = "AssetId",
                Type = "UInt256",
                Value = Output.AssetId.ToString()
            };

            yield return new Variable()
            {
                Name = "Value",
                Type = "long",
                Value = Output.Value.ToString()
            };

            yield return new Variable()
            {
                Name = "ScriptHash",
                Type = "UInt160",
                Value = Output.ScriptHash.ToString()
            };
        }
    }
}
