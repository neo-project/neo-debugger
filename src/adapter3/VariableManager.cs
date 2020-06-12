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

        public static (string typeHint, string text) ParsePrefix(string expression)
        {
            if (expression[0] == '(')
            {
                foreach (var kvp in CastOperations)
                {
                    if (expression.Length > kvp.Key.Length + 2
                        && expression.AsSpan().Slice(1, kvp.Key.Length).SequenceEqual(kvp.Key)
                        && expression[kvp.Key.Length + 1] == ')')
                    {
                        return (kvp.Value, expression.Substring(kvp.Key.Length + 2));
                    }
                }
            }

            return (string.Empty, expression);
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
