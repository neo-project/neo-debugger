using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo.VM;
using NeoDebug.Models;
using NeoDebug.VariableContainers;
using NeoFx;
using NeoFx.Models;
using NeoFx.Storage;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NeoDebug
{

    class DebugExecutionEngine : ExecutionEngine
    {
        private readonly InteropService interopService;

        public DebugExecutionEngine(IScriptContainer container, ScriptTable scriptTable, InteropService interopService)
            : base(container, new Crypto(), scriptTable, interopService)
        {
            this.interopService = interopService;
        }

        public void ExecuteInstruction() => ExecuteNext();

        public IVariableContainer GetStorageContainer(IVariableContainerSession session, in UInt160 scriptHash)
            => interopService.GetStorageContainer(session, scriptHash);

        public EvaluateResponse EvaluateStorageExpression(IVariableContainerSession session, in UInt160 scriptHash, EvaluateArguments args)
            => interopService.EvaluateStorageExpression(session, scriptHash, args);

        public string GetMethodName(uint methodHash) => interopService.GetMethodName(methodHash);
    }
}
