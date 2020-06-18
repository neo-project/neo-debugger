using Neo.VM;
using NeoDebug.ModelAdapters;
using System;

namespace NeoDebug
{
    partial class InteropService
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

        private bool Account_GetBalance(ExecutionEngine engine)
        {
            return engine.TryAdapterOperation<AccountAdapter>(adapter => adapter.GetBalance(engine));
        }

        private bool Account_GetVotes(ExecutionEngine engine)
        {
            return engine.TryAdapterOperation<AccountAdapter>(adapter => adapter.GetVotes(engine));
        }

        private bool Account_GetScriptHash(ExecutionEngine engine)
        {
            return engine.TryAdapterOperation<AccountAdapter>(adapter => adapter.GetScriptHash(engine));
        }

        private bool Account_IsStandard(ExecutionEngine engine)
        {
            throw new NotImplementedException(nameof(Account_IsStandard));
        }
    }
}
