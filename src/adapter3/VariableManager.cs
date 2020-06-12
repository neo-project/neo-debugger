using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace NeoDebug.Neo3
{
    public class VariableManager : IVariableManager
    {
        static readonly IReadOnlyDictionary<string, string> CastOperations = new Dictionary<string, string>()
            {
                { "int", "Integer" },
                { "bool", "Boolean" },
                { "string", "String" },
                { "hex", "HexString" },
                { "byte[]", "ByteArray" },
            }.ToImmutableDictionary();

        static (string typeHint, ReadOnlyMemory<char> text) ParsePrefix(string expression)
        {
            if (expression[0] == '(')
            {
                foreach (var kvp in CastOperations)
                {
                    if (expression.Length > kvp.Key.Length + 2
                        && expression.AsSpan().Slice(1, kvp.Key.Length).SequenceEqual(kvp.Key)
                        && expression[kvp.Key.Length + 1] == ')')
                    {
                        return (kvp.Value, expression.AsMemory().Slice(kvp.Key.Length + 2));
                    }
                }

                throw new Exception("invalid cast operation");
            }

            return (string.Empty, expression.AsMemory());
        }

        static (ReadOnlyMemory<char> name, ReadOnlyMemory<char> remaining) ParseName(ReadOnlyMemory<char> expression)
        {
            for (int i = 0; i < expression.Length; i++)
            {
                char c = expression.Span[i];
                if (c == '.' || c == '[')
                {
                    return (expression.Slice(0, i), expression.Slice(i));
                }
            }

            return (expression, default);
        }

        public static (ReadOnlyMemory<char> name, string typeHint, ReadOnlyMemory<char> remaining) ParseEvalExpression(string expression)
        {
            var (typeHint, text) = ParsePrefix(expression);
            if (text.StartsWith("#storage"))
            {
                return (text, typeHint, default);
            }
            var (name, remaining) = ParseName(text);
            return (name, typeHint, remaining);
        }

        private readonly Dictionary<int, IVariableContainer> containers = new Dictionary<int, IVariableContainer>();

        public void Clear()
        {
            containers.Clear();
        }

        public bool TryGet(int id, [MaybeNullWhen(false)] out IVariableContainer container)
        {
            return containers.TryGetValue(id, out container);
        }

        public int Add(IVariableContainer container)
        {
            var id = container.GetHashCode();
            if (containers.TryAdd(id, container))
            {
                return id;
            }

            throw new Exception();
        }
    }
}
