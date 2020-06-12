using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo;
using Neo.Ledger;
using Neo.Persistence;

namespace NeoDebug.Neo3
{
    class StorageContainer : IVariableContainer
    {
        private readonly StoreView store;
        private readonly int? contractId;

        public StorageContainer(UInt160 scriptHash, StoreView store)
        {
            this.store = store;
            contractId = store.Contracts.TryGet(scriptHash)?.Id;
        }

        bool TryFind(int hashCode, out (StorageKey key, StorageItem item) tuple)
        {
            if (contractId.HasValue)
            {
                foreach (var (key, item) in store.Storages.Find())
                {
                    if (key.Id != contractId.Value)
                        continue;

                    var keyHashCode = key.Key.GetSequenceHashCode();
                    if (hashCode == keyHashCode)
                    {
                        tuple = (key, item);
                        return true;
                    }
                }
            }

            tuple = default;
            return false;
        }

        public IEnumerable<Variable> Enumerate(IVariableManager manager)
        {
            if (contractId.HasValue)
            {
                foreach (var (key, item) in store.Storages.Find())
                {
                    if (key.Id != contractId.Value)
                        continue;

                    var keyHashCode = key.Key.GetSequenceHashCode().ToString("x");
                    var kvp = new KvpContainer(key, item, keyHashCode);
                    yield return new Variable()
                    {
                        Name = keyHashCode,
                        Value = string.Empty,
                        VariablesReference = manager.Add(kvp),
                        NamedVariables = 3
                    };
                }
            }
        }

        static readonly Regex storageRegex = new Regex(@"^\#storage\[([0-9a-fA-F]{8})\]\.(key|item|isConstant)$");

        public EvaluateResponse Evaluate(IVariableManager manager, string expression, string typeHint)
        {
            var match = storageRegex.Match(expression);
            if (match.Success
                && int.TryParse(match.Groups[1].Value, NumberStyles.HexNumber, null, out var keyHash))
            {
                if (TryFind(keyHash, out var tuple))
                {
                    return match.Groups[2].Value switch
                    {
                        "key" => tuple.key.Key
                            .ToVariable(manager, "key", typeHint)
                            .ToEvaluateResponse(),
                        "item" => tuple.item.Value
                            .ToVariable(manager, "item", typeHint)
                            .ToEvaluateResponse(),
                        "isConstant" => tuple.item.IsConstant
                            .ToVariable(manager, "isConstant", typeHint)
                            .ToEvaluateResponse(),
                        _ => DebugAdapter.FailedEvaluation,
                    };
                }
            }

            return DebugAdapter.FailedEvaluation;
        }

        class KvpContainer : IVariableContainer
        {
            private readonly StorageKey key;
            private readonly StorageItem item;
            private readonly string hashCode;

            public KvpContainer(StorageKey key, StorageItem item, string hashCode)
            {
                this.key = key;
                this.item = item;
                this.hashCode = hashCode;
            }

            public IEnumerable<Variable> Enumerate(IVariableManager manager)
            {
                yield return ForEvaluation(key.Key.ToVariable(manager, "key"));
                yield return ForEvaluation(item.Value.ToVariable(manager, "item"));
                yield return ForEvaluation(item.IsConstant.ToVariable(manager, "isConstant"));

                Variable ForEvaluation(Variable variable)
                {
                    variable.EvaluateName = $"#storage[{hashCode}].{variable.Name}";
                    return variable;
                }
            }
        }
    }
}
