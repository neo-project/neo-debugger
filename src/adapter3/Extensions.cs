using System;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;

namespace NeoDebug.Neo3
{
    using StackItem = Neo.VM.Types.StackItem;
    using StackItemType = Neo.VM.Types.StackItemType;

    static class Extensions
    {
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

            Variable NewVar(string value, string type = "")
            {
                return new Variable()
                {
                    Name = name,
                    Value = value,
                    Type = type
                };
            }

            return item switch
            {
                Neo.VM.Types.Boolean _ => NewVar($"{item.ToBoolean()}", "Boolean"),
                Neo.VM.Types.Buffer buffer => NewVar("Buffer"),
                Neo.VM.Types.ByteString byteString => NewVar("ByteString"),
                Neo.VM.Types.Integer @int => NewVar($"{@int.ToBigInteger()}", "Integer"),
                Neo.VM.Types.InteropInterface _ => NewVar("InteropInterface"),
                Neo.VM.Types.Map _ => NewVar("Map"),
                Neo.VM.Types.Null _ => NewVar("null", "Null"),
                Neo.VM.Types.Pointer _ => NewVar("Pointer"),
                // Neo.VM.Types.Struct _ => NewVar("Struct"),
                Neo.VM.Types.Array array => ArrayContainer.Create(manager, array, name),
                _ => throw new NotImplementedException(),
            };
        }
       
    }
}
