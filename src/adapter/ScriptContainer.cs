using Neo.VM;

#nullable enable

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
