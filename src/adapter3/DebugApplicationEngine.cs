using System.Collections.Immutable;
using System.Linq;
using Neo;
using Neo.IO;
using Neo.IO.Caching;
using Neo.Ledger;
using Neo.SmartContract;
using Neo.VM;

namespace NeoDebug.Neo3
{
    class DebugApplicationEngine : ApplicationEngine
    {
        public DebugApplicationEngine() : base(TriggerType.All, null, null, 0, true)
        {
        }
        
        public void ExecuteInstruction() => ExecuteNext();

        protected override bool PreExecuteInstruction() 
        {
            return base.PreExecuteInstruction();
        }
        protected override bool OnSysCall(uint method)
        {
            return base.OnSysCall(method);
        }

        protected override void LoadContext(ExecutionContext context)
        {
            base.LoadContext(context);   
        }
    }
}
