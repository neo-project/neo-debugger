using System.Collections.Generic;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo.SmartContract.Native;

namespace NeoDebug.Neo3
{
    class EngineContainer : IVariableContainer
    {
        readonly IApplicationEngine engine;
        readonly IExecutionContext context;

        public EngineContainer(IApplicationEngine engine, IExecutionContext context)
        {
            this.engine = engine;
            this.context = context;
        }

        public IEnumerable<Variable> Enumerate(IVariableManager manager)
        {
            yield return new Variable
            {
                Name = nameof(IApplicationEngine.GasConsumed),
                Value = engine.GasConsumedAsBigDecimal.ToString(),
            };

            yield return new Variable
            {
                Name = "Current ScriptHash",
                Value = context.ScriptHash.ToString(),
            };

            if (engine is DebugApplicationEngine debugEngine)
            {
                if (engine?.CurrentContext?.ScriptHash != null)
                {
                    var contract = NativeContract.ContractManagement.GetContract(debugEngine.Snapshot, context.ScriptHash);
                    yield return new Variable
                    {
                        Name = "Contract Name",
                        Value = contract?.Manifest.Name ?? "<unknown>"
                    };
                }

                yield return ContractsContainer.Create(manager, debugEngine.Snapshot);
            }
        }
    }
}
