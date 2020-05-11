using System.Collections.Generic;

namespace NeoDebug.Models
{

    public class EventDebugInfo
    {
        public string Id { get; set; } = string.Empty;
        public string Namespace { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public IList<(string Name, string Type)> Parameters { get; set; } = new List<(string, string)>();
    }
}
