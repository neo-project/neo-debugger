using Neo.VM;
using Neo.VM.Types;
using NeoDebug.Adapter.ModelAdapters;
using NeoFx;
using NeoFx.Models;
using NeoFx.Storage;
using System;
using System.Collections.Generic;
using System.Text;

namespace NeoDebug.Adapter
{
    internal partial class InteropService
    {
        public void RegisterAccount(Action<string, Func<ExecutionEngine, bool>, int> register)
        {
            register("Neo.Account.GetScriptHash", Account_GetScriptHash, 1);
            register("Neo.Account.GetVotes", Account_GetVotes, 1);
            register("Neo.Account.GetBalance", Account_GetBalance, 1);

            register("Neo.Account.IsStandard", Account_IsStandard, 100);
            register("AntShares.Account.GetScriptHash", Account_GetScriptHash, 1);
            register("AntShares.Account.GetVotes", Account_GetVotes, 1);
            register("AntShares.Account.GetBalance", Account_GetBalance, 1);
        }

        private bool Account_IsStandard(ExecutionEngine arg)
        {
            throw new NotImplementedException(nameof(Account_IsStandard));
        }

        private bool Account_GetBalance(ExecutionEngine arg)
        {
            throw new NotImplementedException(nameof(Account_GetBalance));
        }

        private bool Account_GetVotes(ExecutionEngine arg)
        {
            throw new NotImplementedException(nameof(Account_GetVotes));
        }

        private bool Account_GetScriptHash(ExecutionEngine arg)
        {
            throw new NotImplementedException(nameof(Account_GetScriptHash));
        }
    }
}
