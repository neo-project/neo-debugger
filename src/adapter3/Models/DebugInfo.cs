using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Neo;
using Neo.BlockchainToolkit.Persistence;
using Neo.IO;
using Neo.Persistence;

namespace NeoDebug.Neo3
{
    public partial class DebugInfo : ISerializable
    {
        public UInt160 ScriptHash { get; set; } = UInt160.Zero;
        public IReadOnlyList<string> Documents { get; set; } = ImmutableList<string>.Empty;
        public IReadOnlyList<Method> Methods { get; set; } = ImmutableList<Method>.Empty;
        public IReadOnlyList<Event> Events { get; set; } = ImmutableList<Event>.Empty;

        // const byte DEBUG_INFO_PREFIX = 0xf0;
        // public static DebugInfo? TryGet(IExpressReadOnlyStore store, UInt160 scriptHash)
        // {
        //     var value = store.TryGet(DEBUG_INFO_PREFIX, scriptHash.ToArray());
        //     return value == null ? null : value.AsSerializable<DebugInfo>();
        // }

        // public void Put(IExpressStore store)
        // {
        //     store.Put(DEBUG_INFO_PREFIX, this.ScriptHash.ToArray(), this.ToArray());
        // }

        // public static IEnumerable<DebugInfo> Find(IExpressReadOnlyStore store)
        // {
        //     return store
        //         .Seek(DEBUG_INFO_PREFIX, Array.Empty<byte>(), SeekDirection.Forward)
        //         .Select(t => t.Value.AsSerializable<DebugInfo>());
        // }

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
            ScriptHash = reader.ReadSerializable<UInt160>();
            var docCount = reader.ReadVarInt();
            var builder = ImmutableList.CreateBuilder<string>();
            for (ulong i = 0; i < docCount; i++)
            {
                builder.Add(reader.ReadVarString());
            }
            Documents = builder.ToImmutable();
            Methods = reader.ReadSerializableArray<Method>();
            Events = reader.ReadSerializableArray<Event>();
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
    }
}
