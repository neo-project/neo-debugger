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
