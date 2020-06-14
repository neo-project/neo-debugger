using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Neo.IO;

namespace NeoDebug.Neo3
{
    public class DebugInfo : ISerializable
    {
        public class SequencePoint : ISerializable
        {
            public int Address { get; set; }
            public int Document { get; set; }
            public (int line, int column) Start { get; set; }
            public (int line, int column) End { get; set; }

            int ISerializable.Size => sizeof(int) * 6;

            void ISerializable.Deserialize(BinaryReader reader)
            {
                Address = reader.ReadInt32();
                Document = reader.ReadInt32();
                var sl = reader.ReadInt32();
                var sc = reader.ReadInt32();
                Start = (sl, sc);
                var el = reader.ReadInt32();
                var ec = reader.ReadInt32();
                End = (el, ec);
            }

            void ISerializable.Serialize(BinaryWriter writer)
            {
                writer.Write(Address);
                writer.Write(Document);
                writer.Write(Start.line);
                writer.Write(Start.column);
                writer.Write(End.line);
                writer.Write(End.column);
            }
        }

        static void WriteTypes(BinaryWriter writer, IReadOnlyList<(string, string)> types)
        {
            writer.Write(types.Count);
            for (int i = 0; i < types.Count; i++)
            {
                writer.WriteVarString(types[i].Item1);
                writer.WriteVarString(types[i].Item2);
            }
        }

        static IEnumerable<(string, string)> ReadTypes(BinaryReader reader)
        {
            var count = reader.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                var item1 = reader.ReadVarString();
                var item2 = reader.ReadVarString();
                yield return (item1, item2);
            }
        }

        static int SizeTypes(IReadOnlyList<(string, string)> types)
        {
            int size = sizeof(int);
            for (int i = 0; i < types.Count; i++)
            {
                size += types[i].Item1.GetVarSize();
                size += types[i].Item2.GetVarSize();
            }
            return size;
        }

        public class Method : ISerializable
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

            int ISerializable.Size => Id.GetVarSize()
                + Namespace.GetVarSize()
                + Name.GetVarSize()
                + ReturnType.GetVarSize()
                + sizeof(int) * 2
                + SizeTypes(Parameters)
                + SizeTypes(Variables)
                + SequencePoints.GetVarSize();

            void ISerializable.Deserialize(BinaryReader reader)
            {
                Id = reader.ReadVarString();
                Namespace = reader.ReadVarString();
                Name = reader.ReadVarString();
                ReturnType = reader.ReadVarString();
                var rs = reader.ReadInt32();
                var re = reader.ReadInt32();
                Range = (rs, re);
                Parameters = ReadTypes(reader).ToImmutableList();
                Variables = ReadTypes(reader).ToImmutableList();
                SequencePoints = ReadSequencePoints().ToImmutableList();

                IEnumerable<SequencePoint> ReadSequencePoints()
                {
                    var count = reader.ReadInt32();
                    for (int i = 0; i < count; i++)
                    {
                        yield return reader.ReadSerializable<SequencePoint>();
                    }
                }
            }

            void ISerializable.Serialize(BinaryWriter writer)
            {
                writer.Write(Id);
                writer.Write(Namespace);
                writer.Write(Name);
                writer.Write(ReturnType);
                writer.Write(Range.Start);
                writer.Write(Range.End);
                WriteTypes(writer, Parameters);
                WriteTypes(writer, Variables);
                writer.Write<SequencePoint>(SequencePoints);
            }
        }

        public class Event : ISerializable
        {
            public string Id { get; set; } = string.Empty;
            public string Namespace { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public IReadOnlyList<(string Name, string Type)> Parameters { get; set; } 
                = ImmutableList<(string, string)>.Empty;

            int ISerializable.Size => Id.GetVarSize()
                + Namespace.GetVarSize()
                + Name.GetVarSize()
                + SizeTypes(Parameters);

            void ISerializable.Deserialize(BinaryReader reader)
            {
                Id = reader.ReadVarString();
                Namespace = reader.ReadVarString();
                Name = reader.ReadVarString();
                Parameters = ReadTypes(reader).ToImmutableList();
            }

            void ISerializable.Serialize(BinaryWriter writer)
            {
                writer.Write(Id);
                writer.Write(Namespace);
                writer.Write(Name);
                WriteTypes(writer, Parameters);
            }
        }

        public Neo.UInt160 ScriptHash { get; set; } = Neo.UInt160.Zero;
        // public string Entrypoint { get; set; } = string.Empty;
        public IReadOnlyList<string> Documents { get; set; } = ImmutableList<string>.Empty;
        public IReadOnlyList<Method> Methods { get; set; } = ImmutableList<Method>.Empty;
        public IReadOnlyList<Event> Events { get; set; } = ImmutableList<Event>.Empty;

        int ISerializable.Size => throw new System.NotImplementedException();

        void ISerializable.Serialize(BinaryWriter writer)
        {
            throw new System.NotImplementedException();
        }

        void ISerializable.Deserialize(BinaryReader reader)
        {
            throw new System.NotImplementedException();
        }
    }
}
