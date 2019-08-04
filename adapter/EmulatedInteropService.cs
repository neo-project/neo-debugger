using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo.VM;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Neo.DebugAdapter
{
    internal class EmulatedInteropService : IInteropService
    {
        private readonly Dictionary<uint, Func<ExecutionEngine, bool>> methods = new Dictionary<uint, Func<ExecutionEngine, bool>>();
        public EmulatedStorage Storage { get; } = new EmulatedStorage();

        public EmulatedInteropService()
        {
            Register("Neo.Storage.GetContext", Storage.GetContext);
            Register("Neo.Storage.Get", Storage.Get);
            Register("Neo.Storage.Put", Storage.Put);
            Register("Neo.Storage.Delete", Storage.Delete);

            Register("System.Storage.GetContext", Storage.GetContext);
            Register("System.Storage.Get", Storage.Get);
            Register("System.Storage.Put", Storage.Put);
            Register("System.Storage.Delete", Storage.Delete);
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
            var hash = InteropMethodHash(methodName);
            methods.Add(hash, handler);
        }
    }
}
