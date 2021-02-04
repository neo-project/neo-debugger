using System.Collections.Generic;
using System.Collections.Immutable;
using Neo;

namespace NeoDebug.Neo3
{
    public partial class DebugInfo
    {
        public UInt160 ScriptHash { get; set; } = UInt160.Zero;
        public IReadOnlyList<string> Documents { get; set; } = ImmutableList<string>.Empty;
        public IReadOnlyList<Method> Methods { get; set; } = ImmutableList<Method>.Empty;
        public IReadOnlyList<Event> Events { get; set; } = ImmutableList<Event>.Empty;

        public class Method
        {
            public string Id { get; set; } = string.Empty;
            public string Namespace { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public (int Start, int End) Range { get; set; }
            public string ReturnType { get; set; } = string.Empty;
            public IReadOnlyList<(string Name, string Type)> Parameters { get; set; }
                = ImmutableList<(string, string)>.Empty;
            public IReadOnlyList<(string Name, string Type)> Variables { get; set; }
                = ImmutableList<(string, string)>.Empty;
            public IReadOnlyList<SequencePoint> SequencePoints { get; set; }
                = ImmutableList<SequencePoint>.Empty;
        }

        public class Event
        {
            public string Id { get; set; } = string.Empty;
            public string Namespace { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public IReadOnlyList<(string Name, string Type)> Parameters { get; set; }
                = ImmutableList<(string, string)>.Empty;
        }

        public class SequencePoint
        {
            public int Address { get; set; }
            public int Document { get; set; }
            public (int line, int column) Start { get; set; }
            public (int line, int column) End { get; set; }
        }
    }
}
