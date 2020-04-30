using System;
using System.Collections.Generic;
using System.Text;

namespace NeoDebug.Models
{
    public class SequencePoint
    {
        public int Address { get; set; }
        public string Document { get; set; } = string.Empty;
        public (int line, int column) Start { get; set; }
        public (int line, int column) End { get; set; }
    }
}
