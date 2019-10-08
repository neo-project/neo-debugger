using Neo.VM;
using NeoFx;
using NeoFx.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace NeoDebug.Adapter
{
    internal partial class InteropService
    {
        public void RegisterBlockchain(Action<string, Func<ExecutionEngine, bool>, int> register)
        {
            register("Neo.Blockchain.GetHeight", Blockchain_GetHeight, 1);
            register("Neo.Blockchain.GetHeader", Blockchain_GetHeader, 100);
            register("Neo.Blockchain.GetBlock", Blockchain_GetBlock, 200);
            register("Neo.Blockchain.GetTransaction", Blockchain_GetTransaction, 100);
            register("Neo.Blockchain.GetTransactionHeight", Blockchain_GetTransactionHeight, 100);
            register("Neo.Blockchain.GetAccount", Blockchain_GetAccount, 100);
            register("Neo.Blockchain.GetValidators", Blockchain_GetValidators, 200);
            register("Neo.Blockchain.GetAsset", Blockchain_GetAsset, 100);
            register("Neo.Blockchain.GetContract", Blockchain_GetContract, 100);

            register("System.Blockchain.GetHeight", Blockchain_GetHeight, 1);
            register("System.Blockchain.GetHeader", Blockchain_GetHeader, 100);
            register("System.Blockchain.GetBlock", Blockchain_GetBlock, 200);
            register("System.Blockchain.GetTransaction", Blockchain_GetTransaction, 200);
            register("System.Blockchain.GetTransactionHeight", Blockchain_GetTransactionHeight, 100);
            register("System.Blockchain.GetContract", Blockchain_GetContract, 100);

            register("AntShares.Blockchain.GetHeight", Blockchain_GetHeight, 1);
            register("AntShares.Blockchain.GetHeader", Blockchain_GetHeader, 100);
            register("AntShares.Blockchain.GetBlock", Blockchain_GetBlock, 200);
            register("AntShares.Blockchain.GetTransaction", Blockchain_GetTransaction, 100);
            register("AntShares.Blockchain.GetAccount", Blockchain_GetAccount, 100);
            register("AntShares.Blockchain.GetValidators", Blockchain_GetValidators, 200);
            register("AntShares.Blockchain.GetAsset", Blockchain_GetAsset, 100);
            register("AntShares.Blockchain.GetContract", Blockchain_GetContract, 100);
        }

        private bool Blockchain_GetContract(ExecutionEngine engine)
        {
            throw new NotImplementedException();
        }

        private bool Blockchain_GetAsset(ExecutionEngine engine)
        {
            throw new NotImplementedException();
        }

        private bool Blockchain_GetValidators(ExecutionEngine engine)
        {
            throw new NotImplementedException();
        }

        private bool Blockchain_GetAccount(ExecutionEngine engine)
        {
            throw new NotImplementedException();
        }

        private bool Blockchain_GetTransactionHeight(ExecutionEngine engine)
        {
            throw new NotImplementedException();
        }

        private bool Blockchain_GetTransaction(ExecutionEngine engine)
        {
            throw new NotImplementedException();
        }

        private bool Blockchain_GetBlock(ExecutionEngine engine)
        {
            throw new NotImplementedException();
        }

        private bool Blockchain_GetHeader(ExecutionEngine engine)
        {
            throw new NotImplementedException();
        }

        private bool Blockchain_GetHeight(ExecutionEngine engine)
        {
            throw new NotImplementedException();
        }
    }
}
