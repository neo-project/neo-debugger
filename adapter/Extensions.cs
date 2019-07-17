using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo.VM;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Neo.DebugAdapter
{
    static class Extensions
    {
        public static string GetResult(this StackItem item)
        {
            switch (item)
            {
                case Neo.VM.Types.Boolean _:
                    return item.GetBoolean().ToString();
                case Neo.VM.Types.Integer _:
                    return item.GetBigInteger().ToString();
                case Neo.VM.Types.ByteArray _:
                    return BitConverter.ToString(item.GetByteArray());
                default:
                    throw new NotImplementedException();
            }
        }
    }
}
