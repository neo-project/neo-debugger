using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo.VM;
using NeoDebug.VariableContainers;
using NeoFx.Models;



namespace NeoDebug.ModelAdapters
{
    class AssetAdapter : AdapterBase, IVariableProvider
    {
        public readonly Asset Item;

        public AssetAdapter(in Asset value)
        {
            Item = value;
        }

        public static AssetAdapter Create(in Asset value)
        {
            return new AssetAdapter(value);
        }

        public bool GetAssetId(ExecutionEngine engine)
        {
            if (Item.AssetId.TryToArray(out var array))
            {
                engine.CurrentContext.EvaluationStack.Push(array);
                return true;
            }

            return false;
        }

        public bool GetAssetType(ExecutionEngine engine)
        {
            engine.CurrentContext.EvaluationStack.Push((int)Item.AssetType);
            return true;
        }

        public bool GetAmount(ExecutionEngine engine)
        {
            engine.CurrentContext.EvaluationStack.Push(Item.Amount.AsRawValue());
            return true;
        }

        public bool GetAvailable(ExecutionEngine engine)
        {
            engine.CurrentContext.EvaluationStack.Push(Item.Available.AsRawValue());
            return true;
        }

        public bool GetPrecision(ExecutionEngine engine)
        {
            engine.CurrentContext.EvaluationStack.Push((int)Item.Precision);
            return true;
        }

        public bool GetOwner(ExecutionEngine engine)
        {
            engine.CurrentContext.EvaluationStack.Push(Item.Owner.Key.ToArray());
            return true;
        }

        public bool GetAdmin(ExecutionEngine engine)
        {
            if (Item.Admin.TryToArray(out var array))
            {
                engine.CurrentContext.EvaluationStack.Push(array);
                return true;
            }

            return false;
        }

        public bool GetIssuer(ExecutionEngine engine)
        {
            if (Item.Issuer.TryToArray(out var array))
            {
                engine.CurrentContext.EvaluationStack.Push(array);
                return true;
            }

            return false;
        }

        public Variable GetVariable(IVariableContainerSession session, string name)
        {
            return new Variable()
            {
                Name = name,
                Type = "Asset",
                Value = string.Empty
            };
        }
    }
}
