using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo.VM;
using NeoDebug.VariableContainers;
using NeoFx.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace NeoDebug.Adapter.ModelAdapters
{

    internal class TransactionAdapter : AdapterBase, IScriptContainer//, IVariableProvider
    {
        public readonly Transaction Transaction;

        public TransactionAdapter(Transaction transaction)
        {
            Transaction = transaction;
        }

        public Variable GetVariable(IVariableContainerSession session)
        {
            return new Variable()
            {
                Type = "Transaction",
                NamedVariables = 1,
            };
        }

        public IEnumerable<Variable> GetVariables(IVariableContainerSession session)
        {
            yield return new Variable()
            {
                Name = "Type",
                Value = Transaction.Type.ToString(),
            };
        }

        byte[] IScriptContainer.GetMessage()
        {
            throw new NotImplementedException();
        }
    }
}
