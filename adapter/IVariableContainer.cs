using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo.VM;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Neo.DebugAdapter
{
    internal interface IVariableContainer
    {
        IEnumerable<Variable> GetVariables(VariablesArguments args);
    }
}
