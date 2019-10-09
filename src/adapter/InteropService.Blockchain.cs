using Neo.VM;
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
            var evalStack = engine.CurrentContext.EvaluationStack;
            var hash = new UInt160(evalStack.Pop().GetByteArray());
            if (blockchain.TryGetContract(hash, out var contract))
            {
                evalStack.Push(StructContainer.ToStackItem(contract));
                return true;
            }

            evalStack.Push(Array.Empty<byte>());
            return true;
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
            var evalStack = engine.CurrentContext.EvaluationStack;
            var hash = new UInt256(evalStack.Pop().GetByteArray());

            if (blockchain.TryGetTransaction(hash, out var index, out var _))
            {
                evalStack.Push(index);
            }
            else
            {
                evalStack.Push(-1);
            }

            return true;
        }

        private bool Blockchain_GetTransaction(ExecutionEngine engine)
        {
            var evalStack = engine.CurrentContext.EvaluationStack;
            var hash = new UInt256(evalStack.Pop().GetByteArray());
            if (blockchain.TryGetTransaction(hash, out var _, out var tx))
            {
                evalStack.Push(StructContainer.ToStackItem(tx));
                return true;
            }

            evalStack.Push(Array.Empty<byte>());
            return true;
        }

        private bool Blockchain_GetBlock(ExecutionEngine engine)
        {
            static bool TryGetBlockHash(IBlockchainStorage blockchain, byte[] data, out UInt256 hash)
            {
                if (data.Length <= 5)
                {
                    var index = (uint)new System.Numerics.BigInteger(data);
                    return blockchain.TryGetBlockHash(index, out hash);
                }

                if (data.Length == UInt256.Size)
                {
                    hash = new UInt256(data);
                    return true;
                }

                hash = default;
                return false;
            }

            var evalStack = engine.CurrentContext.EvaluationStack;
            byte[] data = evalStack.Pop().GetByteArray();
            if (TryGetBlockHash(blockchain, data, out var hash))
            {
                if (blockchain.TryGetBlock(hash, out var block))
                {
                    evalStack.Push(StructContainer.ToStackItem(block));
                }
                else
                {
                    evalStack.Push(Array.Empty<byte>());
                }
            }

            return false;
        }

        private bool Blockchain_GetHeader(ExecutionEngine engine)
        {
            throw new NotImplementedException();
        }

        private bool Blockchain_GetHeight(ExecutionEngine engine)
        {
            engine.CurrentContext.EvaluationStack.Push(blockchain.Height);
            return true;
        }
    }
}
