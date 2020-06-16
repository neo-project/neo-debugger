using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using Neo.IO;

namespace NeoDebug.Neo3
{
    public partial class DebugInfo
    {
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

            public int Size => Id.GetVarSize()
                + Namespace.GetVarSize()
                + Name.GetVarSize()
                + ReturnType.GetVarSize()
                + sizeof(int) * 2
                + SizeTypes(Parameters)
                + SizeTypes(Variables)
                + SequencePoints.GetVarSize();

            public void Deserialize(BinaryReader reader)
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
                SequencePoints = ReadVarEnumeration<SequencePoint>(reader).ToImmutableList();
            }

            public void Serialize(BinaryWriter writer)
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
    }
}
