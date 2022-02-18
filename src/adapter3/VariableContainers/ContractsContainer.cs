using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo.SmartContract;
using Neo.SmartContract.Native;

namespace NeoDebug.Neo3
{
    class ContractsContainer : IVariableContainer
    {
        readonly IReadOnlyList<ContractState> contracts;

        protected ContractsContainer(IReadOnlyList<ContractState> contracts)
        {
            this.contracts = contracts;
        }

        public static Variable Create(IVariableManager manager, Neo.Persistence.DataCache snapshot)
        {
            var contracts = NativeContract.ContractManagement.ListContracts(snapshot)
                .OrderByDescending(cs => cs.Id)
                .ToList();

            var container = new ContractsContainer(contracts);
            return new Variable()
            {
                Name = "Contracts",
                Value = string.Empty,
                VariablesReference = manager.Add(container),
                IndexedVariables = contracts.Count,
            };
        }

        public IEnumerable<Variable> Enumerate(IVariableManager manager)
        {
            for (int i = 0; i < contracts.Count; i++)
            {
                yield return ContractContainer.Create(manager, contracts[i]);
            }
        }

        class ContractContainer : IVariableContainer
        {
            readonly ContractState contract;

            protected ContractContainer(ContractState contract)
            {
                this.contract = contract;
            }

            public static Variable Create(IVariableManager manager, ContractState contract)
            {
                var container = new ContractContainer(contract);
                return new Variable()
                {
                    Name = contract.Manifest.Name,
                    Value = string.Empty,
                    VariablesReference = manager.Add(container),
                    NamedVariables = 3,
                };
            }

            public IEnumerable<Variable> Enumerate(IVariableManager manager)
            {
                yield return new Variable
                {
                    Name = nameof(ContractState.Hash),
                    Value = contract.Hash.ToString(),
                };

                yield return new Variable
                {
                    Name = nameof(ContractState.Id),
                    Value = $"{contract.Id}"
                };

                yield return new Variable
                {
                    Name = nameof(ContractState.UpdateCounter),
                    Value = $"{contract.UpdateCounter}"
                };
            }
        }
    }
}
