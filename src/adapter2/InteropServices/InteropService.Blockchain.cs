using Neo.VM;
using NeoFx;
using NeoFx.Models;
using System;



namespace NeoDebug
{
    partial class InteropService
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

        private bool Blockchain_GetHeight(ExecutionEngine engine)
        {
            if (blockchain != null)
            {
                engine.CurrentContext.EvaluationStack.Push(blockchain.Height);
                return true;
            }
            return false;
        }

        private bool Blockchain_GetContract(ExecutionEngine engine)
        {
            var evalStack = engine.CurrentContext.EvaluationStack;
            var hash = new UInt160(evalStack.Pop().GetByteArray());
            if (blockchain != null
                && blockchain.TryGetContract(hash, out var contract))
            {
                evalStack.Push(new ModelAdapters.DeployedContractAdapter(contract));
                return true;
            }

            evalStack.Push(Array.Empty<byte>());
            return true;
        }

        private bool Blockchain_GetTransactionHeight(ExecutionEngine engine)
        {
            var evalStack = engine.CurrentContext.EvaluationStack;
            var hash = new UInt256(evalStack.Pop().GetByteArray());

            if (blockchain != null
                && blockchain.TryGetTransaction(hash, out var index, out var _))
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
            if (blockchain != null
                && blockchain.TryGetTransaction(hash, out var _, out var tx))
            {
                evalStack.Push(new ModelAdapters.TransactionAdapter(tx));
                return true;
            }

            evalStack.Push(Array.Empty<byte>());
            return true;
        }

        private bool TryGetHash(byte[] data, out UInt256 hash)
        {
            if (data.Length <= 5)
            {
                // treat data as an index
                var index = (uint)new System.Numerics.BigInteger(data);

                // purposefully ignore TryGetBlockHash's return
                // Get_Block/Get_Header will push a null value on the stack
                // if the block hash can't be found.
                if (blockchain != null)
                {
                    var _ = blockchain.TryGetBlockHash(index, out hash);
                }
                else
                {
                    hash = default;
                }

                return true;
            }
            else if (data.Length == 32)
            {
                hash = new UInt256(data);
                return true;
            }

            hash = default;
            return false;
        }

        private bool Blockchain_GetBlock(ExecutionEngine engine)
        {
            var evalStack = engine.CurrentContext.EvaluationStack;
            if (TryGetHash(evalStack.Pop().GetByteArray(), out var hash))
            {
                if (blockchain != null
                    && blockchain.TryGetBlock(hash, out Block block))
                {
                    evalStack.Push(new ModelAdapters.BlockAdapter(block));
                }
                else
                {
                    evalStack.Push(Array.Empty<byte>());
                }
                return true;
            }
            return false;
        }

        private bool Blockchain_GetHeader(ExecutionEngine engine)
        {
            var evalStack = engine.CurrentContext.EvaluationStack;
            if (TryGetHash(evalStack.Pop().GetByteArray(), out var hash))
            {
                if (blockchain != null
                    && blockchain.TryGetBlock(hash, out BlockHeader header, out var _))
                {
                    evalStack.Push(new ModelAdapters.BlockHeaderAdapter(header));
                }
                else
                {
                    evalStack.Push(Array.Empty<byte>());
                }
                return true;
            }
            return false;
        }

        private bool Blockchain_GetAsset(ExecutionEngine engine)
        {
            var evalStack = engine.CurrentContext.EvaluationStack;
            var hash = new UInt256(evalStack.Pop().GetByteArray());
            if (blockchain != null
                && blockchain.TryGetAsset(hash, out Asset asset))
            {
                evalStack.Push(new ModelAdapters.AssetAdapter(asset));
                return true;
            }
            return false;
        }

        private bool Blockchain_GetAccount(ExecutionEngine engine)
        {
            var evalStack = engine.CurrentContext.EvaluationStack;
            var hash = new UInt160(evalStack.Pop().GetByteArray());
            if (blockchain != null
                && blockchain.TryGetAccount(hash, out var account))
            {
                evalStack.Push(new ModelAdapters.AccountAdapter(account));
                return true;
            }

            evalStack.Push(new ModelAdapters.AccountAdapter(new Account(hash)));
            return true;
        }

        private bool Blockchain_GetValidators(ExecutionEngine engine)
        {
            throw new NotImplementedException(nameof(Blockchain_GetValidators));
        }
    }
}
