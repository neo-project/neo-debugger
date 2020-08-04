using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using Neo.IO;

namespace NeoDebug.Neo3
{
    public partial class DebugInfo
    {
        public class Event : ISerializable
        {
            public string Id { get; set; } = string.Empty;
            public string Namespace { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public IReadOnlyList<(string Name, string Type)> Parameters { get; set; }
                = ImmutableList<(string, string)>.Empty;

            public int Size => Id.GetVarSize()
                + Namespace.GetVarSize()
                + Name.GetVarSize()
                + SizeTypes(Parameters);

            public void Deserialize(BinaryReader reader)
            {
                Id = reader.ReadVarString();
                Namespace = reader.ReadVarString();
                Name = reader.ReadVarString();
                Parameters = ReadTypes(reader).ToImmutableList();
            }

            public void Serialize(BinaryWriter writer)
            {
                writer.WriteVarString(Id);
                writer.WriteVarString(Namespace);
                writer.WriteVarString(Name);
                WriteTypes(writer, Parameters);
            }
        }
    }
}
