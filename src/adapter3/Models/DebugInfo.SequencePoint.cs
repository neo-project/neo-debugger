using System.IO;
using Neo.IO;

namespace NeoDebug.Neo3
{
    public partial class DebugInfo
    {
        public class SequencePoint : ISerializable
        {
            public int Address { get; set; }
            public int Document { get; set; }
            public (int line, int column) Start { get; set; }
            public (int line, int column) End { get; set; }

            public int Size => sizeof(int) * 6;

            public void Deserialize(BinaryReader reader)
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

            public void Serialize(BinaryWriter writer)
            {
                writer.Write(Address);
                writer.Write(Document);
                writer.Write(Start.line);
                writer.Write(Start.column);
                writer.Write(End.line);
                writer.Write(End.column);
            }
        }
    }
}
