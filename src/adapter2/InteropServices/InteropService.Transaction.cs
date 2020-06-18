using System;
using Neo.VM;
using NeoDebug.ModelAdapters;



namespace NeoDebug
{
    partial class InteropService
    {
        public void RegisterTransaction(Action<string, Func<ExecutionEngine, bool>, int> register)
        {
            register("Neo.Transaction.GetHash", Transaction_GetHash, 1);
            register("Neo.Transaction.GetType", Transaction_GetType, 1);
            register("Neo.Transaction.GetAttributes", Transaction_GetAttributes, 1);
            register("Neo.Transaction.GetInputs", Transaction_GetInputs, 1);
            register("Neo.Transaction.GetOutputs", Transaction_GetOutputs, 1);
            register("Neo.Transaction.GetReferences", Transaction_GetReferences, 200);
            register("Neo.Transaction.GetUnspentCoins", Transaction_GetUnspentCoins, 200);
            register("Neo.Transaction.GetWitnesses", Transaction_GetWitnesses, 200);
            register("Neo.InvocationTransaction.GetScript", InvocationTransaction_GetScript, 1);
            register("Neo.Input.GetHash", Input_GetHash, 1);
            register("Neo.Input.GetIndex", Input_GetIndex, 1);
            register("Neo.Output.GetAssetId", Output_GetAssetId, 1);
            register("Neo.Output.GetValue", Output_GetValue, 1);
            register("Neo.Output.GetScriptHash", Output_GetScriptHash, 1);
            register("Neo.Witness.GetVerificationScript", Witness_GetVerificationScript, 100);
            register("Neo.Attribute.GetUsage", Attribute_GetUsage, 1);
            register("Neo.Attribute.GetData", Attribute_GetData, 1);

            register("System.Transaction.GetHash", Transaction_GetHash, 1);

            register("AntShares.Transaction.GetHash", Transaction_GetHash, 1);
            register("AntShares.Transaction.GetType", Transaction_GetType, 1);
            register("AntShares.Transaction.GetAttributes", Transaction_GetAttributes, 1);
            register("AntShares.Transaction.GetInputs", Transaction_GetInputs, 1);
            register("AntShares.Transaction.GetOutputs", Transaction_GetOutputs, 1);
            register("AntShares.Transaction.GetReferences", Transaction_GetReferences, 200);
            register("AntShares.Input.GetHash", Input_GetHash, 1);
            register("AntShares.Input.GetIndex", Input_GetIndex, 1);
            register("AntShares.Output.GetAssetId", Output_GetAssetId, 1);
            register("AntShares.Output.GetValue", Output_GetValue, 1);
            register("AntShares.Output.GetScriptHash", Output_GetScriptHash, 1);
            register("AntShares.Attribute.GetUsage", Attribute_GetUsage, 1);
            register("AntShares.Attribute.GetData", Attribute_GetData, 1);
        }

        private bool Attribute_GetData(ExecutionEngine engine)
        {
            return engine.TryAdapterOperation<TransactionAttributeAdapter>(adapter => adapter.GetData(engine));
        }

        private bool Attribute_GetUsage(ExecutionEngine engine)
        {
            return engine.TryAdapterOperation<TransactionAttributeAdapter>(adapter => adapter.GetUsage(engine));
        }

        private bool Witness_GetVerificationScript(ExecutionEngine engine)
        {
            return engine.TryAdapterOperation<WitnessAdapter>(adapter => adapter.GetVerificationScript(engine));
        }

        private bool Transaction_GetWitnesses(ExecutionEngine engine)
        {
            return engine.TryAdapterOperation<TransactionAdapter>(adapter => adapter.GetWitnesses(engine));
        }

        private bool Transaction_GetUnspentCoins(ExecutionEngine engine)
        {
            return engine.TryAdapterOperation<TransactionAdapter>(adapter => adapter.GetUnspentCoins(engine, blockchain));
        }

        private bool Transaction_GetReferences(ExecutionEngine engine)
        {
            return engine.TryAdapterOperation<TransactionAdapter>(adapter => adapter.GetReferences(engine, blockchain));
        }

        private bool Transaction_GetOutputs(ExecutionEngine engine)
        {
            return engine.TryAdapterOperation<TransactionAdapter>(adapter => adapter.GetOutputs(engine));
        }

        private bool Transaction_GetInputs(ExecutionEngine engine)
        {
            return engine.TryAdapterOperation<TransactionAdapter>(adapter => adapter.GetInputs(engine));
        }

        private bool Transaction_GetAttributes(ExecutionEngine engine)
        {
            return engine.TryAdapterOperation<TransactionAdapter>(adapter => adapter.GetAttributes(engine));
        }

        private bool Transaction_GetType(ExecutionEngine engine)
        {
            return engine.TryAdapterOperation<TransactionAdapter>(adapter => adapter.GetType(engine));
        }

        private bool Transaction_GetHash(ExecutionEngine engine)
        {
            return engine.TryAdapterOperation<TransactionAdapter>(adapter => adapter.GetHash(engine));
        }

        private bool InvocationTransaction_GetScript(ExecutionEngine engine)
        {
            return engine.TryAdapterOperation<TransactionAdapter>(adapter => adapter.GetScript(engine));
        }

        private bool Input_GetIndex(ExecutionEngine engine)
        {
            return engine.TryAdapterOperation<CoinReferenceAdapter>(adapter => adapter.GetIndex(engine));
        }

        private bool Input_GetHash(ExecutionEngine engine)
        {
            return engine.TryAdapterOperation<CoinReferenceAdapter>(adapter => adapter.GetHash(engine));
        }

        private bool Output_GetAssetId(ExecutionEngine engine)
        {
            return engine.TryAdapterOperation<TransactionOutputAdatper>(adapter => adapter.GetAssetId(engine));
        }

        private bool Output_GetValue(ExecutionEngine engine)
        {
            return engine.TryAdapterOperation<TransactionOutputAdatper>(adapter => adapter.GetValue(engine));
        }

        private bool Output_GetScriptHash(ExecutionEngine engine)
        {
            return engine.TryAdapterOperation<TransactionOutputAdatper>(adapter => adapter.GetScriptHash(engine));
        }
    }
}
