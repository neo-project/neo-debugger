using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Neo.Cryptography;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.SmartContract;
using StackItem = Neo.VM.Types.StackItem;
using NeoArray = Neo.VM.Types.Array;

namespace NeoDebug.Neo3
{
    using ServiceMethod = Func<DebugApplicationEngine, IReadOnlyList<InteropParameterDescriptor>, StackItem?>;

    class DebugApplicationEngine : ApplicationEngine
    {
        readonly static IReadOnlyDictionary<uint, ServiceMethod> debugServices;

        static DebugApplicationEngine()
        {
            var debugServices = new Dictionary<uint, ServiceMethod>();

            Register("System.Runtime.CheckWitness", Debug_CheckWitness);
            Register("System.Blockchain.GetBlock", Debug_GetBlock);
            Register("System.Blockchain.GetTransactionFromBlock", Debug_GetTransactionFromBlock);
            Register("System.Runtime.Log", Debug_RuntimeLog);
            Register("System.Runtime.Notify", Debug_RuntimeNotify);

            DebugApplicationEngine.debugServices = debugServices;

            void Register(string name, ServiceMethod method)
            {
                var hash = BitConverter.ToUInt32(Encoding.ASCII.GetBytes(name).Sha256(), 0);
                debugServices.Add(hash, method);
            }
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
            if (debugServices.TryGetValue(methodHash, out var method))
            {
                InteropDescriptor descriptor = Services[methodHash];
                ValidateCallFlags(descriptor);
                AddGas(descriptor.FixedPrice);

                var result = method(this, descriptor.Parameters);
                if (result != null)
                {
                    Push(result);
                }
            }
            else
            {
                base.OnSysCall(methodHash);
            }
        }

        private static StackItem? Debug_CheckWitness(
            DebugApplicationEngine engine,
            IReadOnlyList<InteropParameterDescriptor> paramDescriptors)
        {
            Debug.Assert(paramDescriptors.Count == 1);
            _ = (byte[])engine.Convert(engine.Pop(), paramDescriptors[0]);

            return engine.witnessChecker.Check(Neo.UInt160.Zero);
        }

        private static StackItem? Debug_RuntimeNotify(
            DebugApplicationEngine engine,
            IReadOnlyList<InteropParameterDescriptor> paramDescriptors)
        {
            Debug.Assert(paramDescriptors.Count == 2);

            var eventName = (byte[])engine.Convert(engine.Pop(), paramDescriptors[0]);
            var state = (NeoArray)(byte[])engine.Convert(engine.Pop(), paramDescriptors[1]);

            NotifyEventArgs args = new NotifyEventArgs(
                engine.ScriptContainer,
                engine.CurrentScriptHash,
                Neo.Utility.StrictUTF8.GetString(eventName), 
                (NeoArray)state.DeepCopy());
            engine.DebugNotify?.Invoke(engine, args);

            engine.RuntimeNotify(eventName, state);
            return null;
        }

        private static StackItem? Debug_RuntimeLog(
            DebugApplicationEngine engine,
            IReadOnlyList<InteropParameterDescriptor> paramDescriptors)
        {
            Debug.Assert(paramDescriptors.Count == 1);

            var state = (byte[])engine.Convert(engine.Pop(), paramDescriptors[0]);
            var args = new LogEventArgs(
                engine.ScriptContainer,
                engine.CurrentScriptHash,
                Neo.Utility.StrictUTF8.GetString(state));
            engine.DebugLog?.Invoke(engine, args);

            engine.RuntimeLog(state);
            return null;
        }

        private static StackItem? Debug_GetBlock(
            DebugApplicationEngine engine,
            IReadOnlyList<InteropParameterDescriptor> paramDescriptors)
        {
            Debug.Assert(paramDescriptors.Count == 1);

            _ = (byte[])engine.Convert(engine.Pop(), paramDescriptors[0]); // indexOrHash

            throw new InvalidOperationException("System.Blockchain.GetBlock not supported in debugger");
        }

        private static StackItem? Debug_GetTransactionFromBlock(
            DebugApplicationEngine engine,
            IReadOnlyList<InteropParameterDescriptor> paramDescriptors)
        {
            Debug.Assert(paramDescriptors.Count == 2);

            _ = (byte[])engine.Convert(engine.Pop(), paramDescriptors[0]); // blockIndexOrHash
            _ = (int)engine.Convert(engine.Pop(), paramDescriptors[1]); // txIndex

            throw new InvalidOperationException("System.Blockchain.GetTransactionFromBlock not supported in debugger");
        }
    }
}
