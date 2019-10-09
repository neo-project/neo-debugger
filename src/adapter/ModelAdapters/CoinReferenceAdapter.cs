using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo.VM;
using NeoDebug.VariableContainers;
using NeoFx.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace NeoDebug.Adapter.ModelAdapters
{
    internal class CoinReferenceAdapter : AdapterBase, IVariableProvider, IVariableContainer
    {
        public readonly CoinReference Value;

        public CoinReferenceAdapter(in CoinReference value)
        {
            Value = value;
        }

        public static CoinReferenceAdapter Create(in CoinReference value)
        {
            return new CoinReferenceAdapter(value);
        }

        public bool GetIndex(ExecutionEngine engine)
        {
            engine.CurrentContext.EvaluationStack.Push((int)Value.PrevIndex);
            return true;
        }

        public bool GetHash(ExecutionEngine engine)
        {
            if (Value.PrevHash.TryToArray(out var array))
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
                Name = "Input",
                Type = "CoinReference",
                VariablesReference = session.AddVariableContainer(new AdapterVariableContainer(this)),
                NamedVariables = 2,
            };
        }

        public IEnumerable<Variable> GetVariables()
        {
            yield return new Variable()
            {
                Name = "PrevHash",
                Type = "UInt256",
                Value = Value.PrevHash.ToString()
            };
            yield return new Variable()
            {
                Name = "PrevIndex",
                Type = "ushort",
                Value = Value.PrevIndex.ToString()
            };
        }
    }
}
