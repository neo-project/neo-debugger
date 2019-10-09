using Neo.VM;

#nullable enable

namespace NeoDebug.Adapter
{
    static class StructContainer
    {
        public static StackItem ToStackItem<T>(in T item) where T : struct
        {
            return StackItem.FromInterface(new StructContainer<T>(item));
        }
    }

    class StructContainer<T> : IScriptContainer where T : struct
    {
        public delegate byte[] GetMessageDelegate(in T item);

        public readonly T Item;
        private readonly GetMessageDelegate? getMessage;

        public StructContainer(in T item, GetMessageDelegate? getMessage = null)
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
