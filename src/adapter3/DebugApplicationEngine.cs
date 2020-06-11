using System;
using System.Collections.Immutable;
using System.Linq;
using Neo;
using Neo.IO;
using Neo.IO.Caching;
using Neo.Ledger;
using Neo.Persistence;
using Neo.SmartContract;
using Neo.VM;

namespace NeoDebug.Neo3
{
    class DebugApplicationEngine : ApplicationEngine
    {
        static readonly uint CheckWitnessHash = "System.Runtime.CheckWitness".ToInteropMethodHash();

        public DebugApplicationEngine(StoreView storeView) : base(TriggerType.Application, null, storeView, 0, true)
        {
        }
        
        public void ExecuteInstruction() => ExecuteNext();

        protected override bool PreExecuteInstruction() 
        {
            return base.PreExecuteInstruction();
        }

        protected override bool OnSysCall(uint method)
        {
            if (method == CheckWitnessHash)
            {
                return Runtime_CheckWitness(this);
            }

            return base.OnSysCall(method);
        }

        private static bool Runtime_CheckWitness(ApplicationEngine engine)
        {
            var _ = engine.CurrentContext.EvaluationStack.Pop().GetSpan();
            engine.CurrentContext.EvaluationStack.Push(true);
            return true;
        }

        protected override void LoadContext(ExecutionContext context)
        {
            base.LoadContext(context);   
        }
    }
}
