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

            Variable ToVariable(string value, string type = "")
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
                Neo.VM.Types.Boolean _ => ToVariable($"{item.ToBoolean()}", "Boolean"),
                Neo.VM.Types.Buffer buffer => ToVariable("Buffer"),
                Neo.VM.Types.ByteString byteString => ToVariable("ByteString"),
                Neo.VM.Types.Integer @int => ToVariable($"{@int.ToBigInteger()}", "Integer"),
                Neo.VM.Types.InteropInterface _ => ToVariable("InteropInterface"),
                Neo.VM.Types.Map _ => ToVariable("Map"),
                Neo.VM.Types.Null _ => ToVariable("null", "Null"),
                Neo.VM.Types.Pointer _ => ToVariable("Pointer"),
                Neo.VM.Types.Array array => NeoArrayContainer.Create(manager, array, name),
                _ => throw new NotImplementedException(),
            };
        }
       
    }
}
