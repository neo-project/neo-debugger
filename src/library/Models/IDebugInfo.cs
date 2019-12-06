using System;
using System.Collections.Generic;
using System.Text;

namespace NeoDebug.Models
{
    public interface IMethod
    {
        public string Id { get; }
        public (string @namespace, string name) Name { get; }
        public (int start, int end) Range { get; }
        public IReadOnlyList<(string name, string type)> Parameters { get; }
        public string ReturnType { get; }
        public IReadOnlyList<(string name, string type)> Variables { get; }
        public IReadOnlyList<ISequencePoint> SequencePoints { get; }
    }

    public interface IEvent
    {
        public string Id { get; }
        public (string @namespace, string name) Name { get; }
        public IReadOnlyList<(string name, string type)> Parameters { get; }
    }

    public interface ISequencePoint
    {
        public int Address { get; }
        public string Document { get; }
        public (int line, int column) Start { get; }
        public (int line, int column) End { get; }
    }

    public interface IDebugInfo
    {
        public string Entrypoint { get; }
        public IReadOnlyList<IMethod> Methods { get; }
        public IReadOnlyList<IEvent> Events { get; }
    }
}
