using System.Collections.Generic;

namespace NeoDebug.Models
{
    public class MethodDebugInfo
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
}
