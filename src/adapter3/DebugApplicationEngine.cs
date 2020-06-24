using System;
using System.Collections.Generic;
using System.Text;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.SmartContract;
using Neo.VM;

namespace NeoDebug.Neo3
{
    class DebugApplicationEngine : ApplicationEngine
    {
        readonly static IReadOnlyDictionary<uint, Func<uint, DebugApplicationEngine, bool>> methods;

        static DebugApplicationEngine()
        {
            var methods = new Dictionary<uint, Func<uint, DebugApplicationEngine, bool>>();

            Register("System.Runtime.CheckWitness", Runtime_CheckWitness);
            Register("System.Blockchain.GetBlock", Blockchain_GetBlock);
            Register("System.Blockchain.GetTransactionFromBlock", Blockchain_GetTransactionFromBlock);
            Register("System.Runtime.Log", Runtime_Log);
            Register("System.Runtime.Notify", Runtime_Notify);

            DebugApplicationEngine.methods = methods;
            
            void Register(string methodName, Func<uint, DebugApplicationEngine, bool> func)
            {
                methods.Add(methodName.ToInteropMethodHash(), func);
            }
        }

        public event EventHandler<NotifyEventArgs>? DebugNotify;
        public event EventHandler<LogEventArgs>? DebugLog;

        public DebugApplicationEngine(IVerifiable container, StoreView storeView) : base(TriggerType.Application, container, storeView, 0, true)
        {
        }
        
        public void ExecuteInstruction() => ExecuteNext();

        protected override bool PreExecuteInstruction() 
        {
            return base.PreExecuteInstruction();
        }

        protected override bool OnSysCall(uint methodHash)
        {
            if (methods.TryGetValue(methodHash, out var method))
            {
                return method(methodHash, this);
            }

            return base.OnSysCall(methodHash);
        }

        private bool BaseOnSysCall(uint methodHash) => base.OnSysCall(methodHash);

        private void InvokeDebugLog(string message)
        {
            var log = new LogEventArgs(this.ScriptContainer, this.CurrentScriptHash, message);
            this.DebugLog?.Invoke(this, log);
        }

        private static bool Runtime_CheckWitness(uint methodHash, ApplicationEngine engine)
        {
            var _ = engine.CurrentContext.EvaluationStack.Pop().GetSpan();
            engine.CurrentContext.EvaluationStack.Push(true);
            return true;
        }

        private static bool Runtime_Notify(uint methodHash, DebugApplicationEngine engine)
        {
            var state = engine.CurrentContext.EvaluationStack.Peek(0);
            var notification = new NotifyEventArgs(engine.ScriptContainer, engine.CurrentScriptHash, state);
            engine.DebugNotify?.Invoke(engine, notification);
            return engine.BaseOnSysCall(methodHash);
        }

        private static bool Runtime_Log(uint methodHash, DebugApplicationEngine engine)
        {
            ReadOnlySpan<byte> state = engine.CurrentContext.EvaluationStack.Peek(0).GetSpan();
            string message = Encoding.UTF8.GetString(state);
            engine.InvokeDebugLog(message);
            return engine.BaseOnSysCall(methodHash);
        }

        private static bool Blockchain_GetTransactionFromBlock(uint methodHash, DebugApplicationEngine engine)
        {
            engine.InvokeDebugLog("System.Blockchain.GetTransactionFromBlock not supported in debugger");
            return false;
        }

        private static bool Blockchain_GetBlock(uint methodHash, DebugApplicationEngine engine)
        {
            engine.InvokeDebugLog("System.Blockchain.GetBlock not supported in debugger");
            return false;
        }
    }
}
