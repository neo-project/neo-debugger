using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo.VM;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Neo.DebugAdapter
{

    class NeoDebugSession
    {
        public Contract Contract;
        public ContractParameter[] Arguments;
        public ScriptTable ScriptTable = new ScriptTable();
        public ExecutionEngine Engine;
        public Debugger Debugger;

        public NeoDebugSession(Contract contract, IEnumerable<ContractParameter> arguments)
        {
            Contract = contract;
            Arguments = arguments.ToArray();
            ScriptTable.Add(Contract);
            var builder = contract.BuildInvokeScript(Arguments);
            Engine = new ExecutionEngine(null, new Crypto(), ScriptTable);
            Engine.LoadScript(builder.ToArray());
            Debugger = new Debugger(Engine);

            for (var x = 0; x < Contract.SequencePoints.Length; x++)
            {
                Debugger.AddBreakPoint(contract.ScriptHash, Contract.SequencePoints[x].Address);
            }
        }

        public void Execute()
        {
            Debugger.Execute();
        }

        public IEnumerable<StackFrame> GetStackFrames()
        {
            if (Engine.State == VMState.BREAK)
            {
                var sp = Contract.SequencePoints
                    .SingleOrDefault(_sp => _sp.Address == Engine.CurrentContext.InstructionPointer);

                if (sp != null)
                {
                    yield return new StackFrame()
                    {
                        Id = 0,
                        Name = "frame 0",
                        Source = new Source()
                        {
                            Name = Path.GetFileName(sp.Document),
                            Path = sp.Document
                        },
                        Line = sp.Start.line,
                        Column = sp.Start.column,
                        EndLine = sp.End.line,
                        EndColumn = sp.End.column,
                    };
                }
            }
        }
    }
}
