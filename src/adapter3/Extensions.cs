using System;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo;

namespace NeoDebug.Neo3
{
    using StackItem = Neo.VM.Types.StackItem;

    static class Extensions
    {
        //https://stackoverflow.com/a/1646913
        public static int GetSequenceHashCode(this ReadOnlySpan<byte> span)
        {
            unchecked
            {
                int hash = 17;
                for (int i = 0; i < span.Length; i++)
                {
                    hash = hash * 31 + span[i];
                }
                return hash;
            }
        }

        public static int GetSequenceHashCode(this byte[] array)
        {
            return GetSequenceHashCode(array.AsSpan());
        }
        
        public static string ToResult(this StackItem item)
        {
            return item switch
            {
                Neo.VM.Types.Boolean _ => item.ToBoolean().ToString(),
                Neo.VM.Types.Buffer buffer => "Buffer",
                Neo.VM.Types.ByteString byteString => byteString.Span.ToHexString(), 
                Neo.VM.Types.Integer @int => @int.ToBigInteger().ToString(),
                // Neo.VM.Types.InteropInterface _ => MakeVariable("InteropInterface"),
                // Neo.VM.Types.Map _ => MakeVariable("Map"),
                Neo.VM.Types.Null _ => "<null>",
                // Neo.VM.Types.Pointer _ => MakeVariable("Pointer"),
                Neo.VM.Types.Array array => "NeoArray",
                _ => throw new NotImplementedException(),
            };
        }
        
        public static Variable ToVariable(this StackItem item, IVariableManager manager, string name, string? typeHint = null)
        {
            // switch (typeHint)
            // {
            //     case "Boolean":
            //         return new Variable()
            //         {
            //             Name = name,
            //             Value = item.GetBoolean().ToString(),
            //             Type = "#Boolean",
            //         };
            //     case "Integer":
            //         return new Variable()
            //         {
            //             Name = name,
            //             Value = item.GetBigInteger().ToString(),
            //             Type = "#Integer",
            //         };
            //     case "String":
            //         return new Variable()
            //         {
            //             Name = name,
            //             Value = item.GetString(),
            //             Type = "#String",
            //         };
            //     case "HexString":
            //         return new Variable()
            //         {
            //             Name = name,
            //             Value = item.GetBigInteger().ToHexString(),
            //             Type = "#ByteArray"
            //         };
            //     case "ByteArray":
            //         return ByteArrayContainer.Create(session, item.GetByteArray(), name, true);
            // }

            return item switch
            {
                Neo.VM.Types.Boolean _ => MakeVariable($"{item.ToBoolean()}", "Boolean"),
                Neo.VM.Types.Buffer buffer => ByteArrayContainer.Create(manager, buffer, name),
                Neo.VM.Types.ByteString byteString => ByteArrayContainer.Create(manager, byteString, name),
                Neo.VM.Types.Integer @int => MakeVariable($"{@int.ToBigInteger()}", "Integer"),
                Neo.VM.Types.InteropInterface _ => MakeVariable("InteropInterface"),
                Neo.VM.Types.Map _ => MakeVariable("Map"),
                Neo.VM.Types.Null _ => MakeVariable("null", "Null"),
                Neo.VM.Types.Pointer _ => MakeVariable("Pointer"),
                Neo.VM.Types.Array array => NeoArrayContainer.Create(manager, array, name),
                _ => throw new NotImplementedException(),
            };

            Variable MakeVariable(string value, string type = "")
            {
                return new Variable()
                {
                    Name = name,
                    Value = value,
                    Type = type
                };
            }
        }
    }
}
