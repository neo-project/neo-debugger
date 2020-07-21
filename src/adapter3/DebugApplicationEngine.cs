using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Neo;
using Neo.Cryptography.ECC;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.SmartContract;
using Neo.VM;

namespace NeoDebug.Neo3
{
    class DebugApplicationEngine : ApplicationEngine
    {

        readonly static IReadOnlyDictionary<uint, InteropDescriptor> services = new Dictionary<uint, InteropDescriptor>();

        static DebugApplicationEngine()
        {
            // Register("System.Runtime.CheckWitness", Runtime_CheckWitness);
            // Register("System.Blockchain.GetBlock", Blockchain_GetBlock);
            // Register("System.Blockchain.GetTransactionFromBlock", Blockchain_GetTransactionFromBlock);
            // Register("System.Runtime.Log", Runtime_Log);
            // Register("System.Runtime.Notify", Runtime_Notify);

            // DebugApplicationEngine.methods = methods;
            
            // void Register(string name, string handler)
            // {
            //     var hash = BitConverter.ToUInt32(Encoding.ASCII.GetBytes(name).Sha256(), 0);
            //     methods.Add(hash, func);
            // }
        }

        public event EventHandler<NotifyEventArgs>? DebugNotify;
        public event EventHandler<LogEventArgs>? DebugLog;
        private readonly WitnessChecker witnessChecker;

        public DebugApplicationEngine(IVerifiable container, StoreView storeView, WitnessChecker witnessChecker) : base(TriggerType.Application, container, storeView, 0, true)
        {
            this.witnessChecker = witnessChecker;
        }
        
        public void ExecuteInstruction() => ExecuteNext();

        protected override void OnSysCall(uint methodHash)
        {
            // if (methods.TryGetValue(methodHash, out var method))
            // {
            //     method(methodHash, this);
            // }
            // else
            // {
            //     base.OnSysCall(methodHash);
            // }
        }

        private void BaseOnSysCall(uint methodHash) => base.OnSysCall(methodHash);

        private void InvokeDebugLog(string message)
        {
            var log = new LogEventArgs(this.ScriptContainer, this.CurrentScriptHash, message);
            this.DebugLog?.Invoke(this, log);
        }


        // internal bool CheckWitness(byte[] hashOrPubkey)
        private static bool Runtime_CheckWitness(uint methodHash, DebugApplicationEngine engine)
        {
            ReadOnlySpan<byte> hashOrPubkey = engine.CurrentContext.EvaluationStack.Pop().GetSpan();
            var hash = hashOrPubkey.Length switch
            {
                20 => new UInt160(hashOrPubkey),
                33 => Contract.CreateSignatureRedeemScript(ECPoint.DecodePoint(hashOrPubkey, ECCurve.Secp256r1)).ToScriptHash(),
                _ => null
            };
            if (hash is null) return false;

            engine.CurrentContext.EvaluationStack.Push(engine.witnessChecker.Check(hash));
            return true;
        }

        // internal void RuntimeNotify(byte[] eventName, Array state)
        // private static bool Runtime_Notify(uint methodHash, DebugApplicationEngine engine)
        // {
        //     // var state = engine.CurrentContext.EvaluationStack.Peek(0);
        //     // var notification = new NotifyEventArgs(engine.ScriptContainer, engine.CurrentScriptHash, state);
        //     // engine.DebugNotify?.Invoke(engine, notification);
        //     // return engine.BaseOnSysCall(methodHash);
        // }

        // internal void RuntimeLog(byte[] state)
        // private static bool Runtime_Log(uint methodHash, DebugApplicationEngine engine)
        // {
        //     // ReadOnlySpan<byte> state = engine.CurrentContext.EvaluationStack.Peek(0).GetSpan();
        //     // string message = Encoding.UTF8.GetString(state);
        //     // engine.InvokeDebugLog(message);
        //     // return engine.BaseOnSysCall(methodHash);
        // }

        // internal Transaction GetTransactionFromBlock(byte[] blockIndexOrHash, int txIndex)
        // private static bool Blockchain_GetTransactionFromBlock(uint methodHash, DebugApplicationEngine engine)
        // {
        //     engine.InvokeDebugLog("System.Blockchain.GetTransactionFromBlock not supported in debugger");
        //     return false;
        // }

        // internal Block GetBlock(byte[] indexOrHash)
        // private static bool Blockchain_GetBlock(uint methodHash, DebugApplicationEngine engine)
        // {
        //     engine.InvokeDebugLog("System.Blockchain.GetBlock not supported in debugger");
        //     return false;
        // }
    }
}
