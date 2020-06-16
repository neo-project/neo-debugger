using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Neo.IO;

namespace NeoDebug.Neo3
{
    public partial class DebugInfo : ISerializable
    {
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

        static IEnumerable<T> ReadVarEnumeration<T>(BinaryReader reader) where T : ISerializable, new()
        {
            var count = reader.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                yield return reader.ReadSerializable<T>();
            }
        }

        public Neo.UInt160 ScriptHash { get; set; } = Neo.UInt160.Zero;
        public IReadOnlyList<string> Documents { get; set; } = ImmutableList<string>.Empty;
        public IReadOnlyList<Method> Methods { get; set; } = ImmutableList<Method>.Empty;
        public IReadOnlyList<Event> Events { get; set; } = ImmutableList<Event>.Empty;

        public int Size => ScriptHash.Size
            + Documents.GetVarSize()
            + Methods.GetVarSize()
            + Events.GetVarSize();

        public void Serialize(BinaryWriter writer)
        {
            writer.Write(ScriptHash);
            writer.WriteVarInt(Documents.Count);
            for (int i = 0; i < Documents.Count; i++)
            {
                writer.WriteVarString(Documents[i]);
            }
            writer.Write<Method>(Methods);
            writer.Write<Event>(Events);
        }

        public void Deserialize(BinaryReader reader)
        {
            ScriptHash = reader.ReadSerializable<Neo.UInt160>();
            var docCount = reader.ReadVarInt();
            var builder = ImmutableList.CreateBuilder<string>();
            for (ulong i = 0; i < docCount; i++)
            {
                builder.Add(reader.ReadVarString());
            }
            Documents = builder.ToImmutable();
            Methods = ReadVarEnumeration<Method>(reader).ToImmutableList();
            Events = ReadVarEnumeration<Event>(reader).ToImmutableList();
        }
    }
}
