using Neo.VM;

namespace NeoDebug.Neo3
{
    class DebugExecutionEngine : ExecutionEngine
    {
        public DebugExecutionEngine() : base()
        {
        }
        
        public void ExecuteInstruction() => ExecuteNext();
    }
}
