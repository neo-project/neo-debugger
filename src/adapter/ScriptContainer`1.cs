using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo.VM;
using NeoDebug.Models;
using NeoDebug.VariableContainers;
using NeoFx;
using NeoFx.Models;
using NeoFx.Storage;
using Newtonsoft.Json.Linq;
using OneOf;
using System.Collections.Generic;
using System.Linq;

namespace NeoDebug.Adapter
{
    class ScriptContainer<T> : IScriptContainer
    {
        public delegate byte[] GetMessageDelegate(in T item);

        public readonly T Item;
        private readonly GetMessageDelegate? getMessage;

        public ScriptContainer(in T item, GetMessageDelegate? getMessage = null)
        {
            Item = item;
            this.getMessage = getMessage;
        }

        public byte[] GetMessage()
        {
            if (getMessage != null)
            {
                return getMessage(Item);
            }

            throw new System.NotImplementedException();
        }
    }
}
