using System.Collections.Generic;

namespace NeoDebug.Models
{
    public class DebugInfo
    {
        public class SequencePoint
        {
            public int Address { get; set; }
            public string Document { get; set; } = string.Empty;
            public (int line, int column) Start { get; set; }
            public (int line, int column) End { get; set; }
        }

        public class Method
        {
            public string Id { get; set; } = string.Empty;
            public string Namespace { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public (int Start, int End) Range { get; set; }
            public IList<(string Name, string Type)> Parameters { get; set; } = new List<(string, string)>();
            public string ReturnType { get; set; } = string.Empty;
            public IList<(string Name, string Type)> Variables { get; set; } = new List<(string, string)>();
            public IList<SequencePoint> SequencePoints { get; set; } = new List<SequencePoint>();
        }

        public class Event
        {
            public string Id { get; set; } = string.Empty;
            public string Namespace { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public IList<(string Name, string Type)> Parameters { get; set; } = new List<(string, string)>();
        }

        public string Entrypoint { get; set; } = string.Empty;
        public IList<Method> Methods { get; set; } = new List<Method>();
        public IList<Event> Events { get; set; } = new List<Event>();
    }
}
