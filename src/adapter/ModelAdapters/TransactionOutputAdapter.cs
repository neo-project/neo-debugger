using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo.VM;
using NeoDebug.VariableContainers;
using NeoFx.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace NeoDebug.Adapter.ModelAdapters
{
    internal class TransactionOutputAdatper : AdapterBase, IVariableProvider, IVariableContainer
    {
        public readonly TransactionOutput Value;

        public TransactionOutputAdatper(in TransactionOutput value)
        {
            Value = value;
        }

        public static TransactionOutputAdatper Create(in TransactionOutput value)
        {
            return new TransactionOutputAdatper(value);
        }

        public bool GetAssetId(ExecutionEngine engine)
        {
            if (Value.AssetId.TryToArray(out var array))
            {
                engine.CurrentContext.EvaluationStack.Push(array);
                return true;
            }

            return false;
        }

        public bool GetValue(ExecutionEngine engine)
        {
            engine.CurrentContext.EvaluationStack.Push(Value.Value);
            return true;
        }

        public bool GetScriptHash(ExecutionEngine engine)
        {
            if (Value.ScriptHash.TryToArray(out var array))
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
                Value = Value.AssetId.ToString()
            };

            yield return new Variable()
            {
                Name = "Value",
                Type = "long",
                Value = Value.Value.ToString()
            };

            yield return new Variable()
            {
                Name = "ScriptHash",
                Type = "UInt160",
                Value = Value.ScriptHash.ToString()
            };
        }
    }
}
