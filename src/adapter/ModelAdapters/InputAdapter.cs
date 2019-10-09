using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo.VM;
using NeoDebug.VariableContainers;
using NeoFx.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace NeoDebug.Adapter.ModelAdapters
{
    internal class InputAdapter : AdapterBase, IVariableProvider, IVariableContainer
    {
        public readonly CoinReference Input;

        public InputAdapter(CoinReference input)
        {
            Input = input;
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
                Value = Input.PrevHash.ToString()
            };
            yield return new Variable()
            {
                Name = "PrevIndex",
                Type = "ushort",
                Value = Input.PrevIndex.ToString()
            };
        }

        public bool GetIndex(ExecutionEngine engine)
        {
            engine.CurrentContext.EvaluationStack.Push((int)Input.PrevIndex);
            return true;
        }

        public bool GetHash(ExecutionEngine engine)
        {
            if (Input.PrevHash.TryToArray(out var array))
            {
                engine.CurrentContext.EvaluationStack.Push(array);
                return true;
            }

            return false;
        }
    }
}
