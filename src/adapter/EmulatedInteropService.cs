using Neo.VM;
using System;
using System.Collections.Generic;
using System.Text;

namespace NeoDebug.Adapter
{
    internal class EmulatedInteropService : IInteropService
    {
        private readonly Dictionary<uint, Func<ExecutionEngine, bool>> methods = new Dictionary<uint, Func<ExecutionEngine, bool>>();
        private readonly EmulatedStorage storage;
        private readonly EmulatedRuntime runtime;

        public EmulatedInteropService(EmulatedStorage storage, EmulatedRuntime runtime)
        {
            if (storage == null)
                throw new ArgumentNullException(nameof(storage));

            if (runtime == null)
                throw new ArgumentNullException(nameof(runtime));

            this.storage = storage;
            this.runtime = runtime;

            storage.RegisterServices(Register);
            runtime.RegisterServices(Register);
        }

        public bool Invoke(byte[] method, ExecutionEngine engine)
        {
            uint hash = method.Length == 4
               ? BitConverter.ToUInt32(method, 0)
               : InteropMethodHash(Encoding.ASCII.GetString(method));

            if (methods.TryGetValue(hash, out var func))
            {
                return func(engine);
            }

            return false;
        }

        static uint InteropMethodHash(string methodName)
        {
            var asciiMethodName = Encoding.ASCII.GetBytes(methodName);
            var asciiMethodNameHash = Crypto.SHA256.Value.ComputeHash(asciiMethodName);
            return BitConverter.ToUInt32(asciiMethodNameHash, 0);
        }

        protected void Register(string methodName, Func<ExecutionEngine, bool> handler)
        {
            methods.Add(InteropMethodHash(methodName), handler);
        }
    }
}
