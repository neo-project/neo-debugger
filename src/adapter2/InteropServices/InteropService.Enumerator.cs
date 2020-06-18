using Neo.VM;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;



namespace NeoDebug
{
    partial class InteropService
    {
        interface IEnumerator
        {
            bool MoveNext();
            StackItem Value { get; }
        }

        interface IIterator : IEnumerator
        {
            StackItem Key { get; }
        }

        class Iterator : IIterator
        {
            private readonly IEnumerator<(StackItem key, StackItem value)> enumerator;

            public Iterator(IEnumerator<(StackItem, StackItem)> enumerator)
            {
                this.enumerator = enumerator;
            }

            public Iterator(IEnumerable<(StackItem, StackItem)> enumerable)
            {
                enumerator = enumerable.GetEnumerator();
            }

            public static IIterator Create(Neo.VM.Types.Array array)
            {
                var enumerable = array.Select((item, index) => ((StackItem)index, item));
                return new Iterator(enumerable);
            }

            public static IIterator Create(Neo.VM.Types.Map map)
            {
                var enumerable = map.Select(kvp => (kvp.Key, kvp.Value));
                return new Iterator(enumerable);
            }

            public StackItem Key => enumerator.Current.key;

            public StackItem Value => enumerator.Current.value;

            public bool MoveNext() => enumerator.MoveNext();
        }

        public void RegisterEnumerator(Action<string, Func<ExecutionEngine, bool>, int> register)
        {
            register("Neo.Enumerator.Create", Enumerator_Create, 1);
            register("Neo.Enumerator.Next", Enumerator_Next, 1);
            register("Neo.Enumerator.Value", Enumerator_Value, 1);
            register("Neo.Enumerator.Concat", Enumerator_Concat, 1);

            register("Neo.Iterator.Create", Iterator_Create, 1);
            register("Neo.Iterator.Key", Iterator_Key, 1);
            register("Neo.Iterator.Keys", Iterator_Keys, 1);
            register("Neo.Iterator.Values", Iterator_Values, 1);
            register("Neo.Iterator.Concat", Iterator_Concat, 1);
            register("Neo.Iterator.Next", Enumerator_Next, 1);
            register("Neo.Iterator.Value", Enumerator_Value, 1);
        }

        private bool Enumerator_Create(ExecutionEngine engine)
        {
            if (engine.CurrentContext.EvaluationStack.Pop() is Neo.VM.Types.Array array)
            {
                engine.CurrentContext.EvaluationStack.Push(
                    StackItem.FromInterface(Iterator.Create(array)));
                return true;
            }
            return false;
        }

        private bool Enumerator_Next(ExecutionEngine engine)
        {
            if (engine.CurrentContext.EvaluationStack.Pop() is Neo.VM.Types.InteropInterface @interface)
            {
                var enumerator = @interface.GetInterface<IEnumerator>();
                engine.CurrentContext.EvaluationStack.Push(enumerator.MoveNext());
                return true;
            }
            return false;
        }

        private bool Enumerator_Value(ExecutionEngine engine)
        {
            if (engine.CurrentContext.EvaluationStack.Pop() is Neo.VM.Types.InteropInterface @interface)
            {
                var enumerator = @interface.GetInterface<IEnumerator>();
                engine.CurrentContext.EvaluationStack.Push(enumerator.Value);
                return true;
            }
            return false;
        }

        private static bool TryGetIterator(RandomAccessStack<StackItem> evalStack, [NotNullWhen(true)] out IIterator? iterator)
        {
            switch (evalStack.Pop())
            {
                case Neo.VM.Types.Array array:
                    iterator = Iterator.Create(array);
                    return true;
                case Neo.VM.Types.Map map:
                    iterator = Iterator.Create(map);
                    return true;
                default:
                    iterator = default;
                    return false;
            }
        }

        private bool Iterator_Create(ExecutionEngine engine)
        {
            var evalStack = engine.CurrentContext.EvaluationStack;
            if (TryGetIterator(evalStack, out var iterator))
            {
                evalStack.Push(StackItem.FromInterface(iterator));
                return true;
            }

            return false;
        }

        private bool Iterator_Key(ExecutionEngine engine)
        {
            if (engine.CurrentContext.EvaluationStack.Pop() is Neo.VM.Types.InteropInterface @interface)
            {
                var iterator = @interface.GetInterface<IIterator>();
                engine.CurrentContext.EvaluationStack.Push(iterator.Key);
                return true;
            }
            return false;
        }

        private bool Enumerator_Concat(ExecutionEngine engine)
        {
            throw new NotImplementedException(nameof(Enumerator_Concat));
        }

        private bool Iterator_Concat(ExecutionEngine engine)
        {
            throw new NotImplementedException(nameof(Iterator_Concat));
        }

        private bool Iterator_Keys(ExecutionEngine engine)
        {
            throw new NotImplementedException(nameof(Iterator_Keys));
        }

        private bool Iterator_Values(ExecutionEngine engine)
        {
            throw new NotImplementedException(nameof(Iterator_Values));
        }
    }
}
