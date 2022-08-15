using System.Collections.Generic;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;

namespace NeoDebug.Neo3
{
    class EngineContainer : IVariableContainer
    {
        readonly IApplicationEngine engine;

        public EngineContainer(IApplicationEngine engine)
        {
            this.engine = engine;
        }

        public IEnumerable<Variable> Enumerate(IVariableManager manager)
        {
            yield return new Variable
            {
                Name = nameof(IApplicationEngine.GasConsumed),
                Value = engine.GasConsumed.AsBigDecimal().ToString(),
            };
        }
    }
}
