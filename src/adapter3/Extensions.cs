using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NeoDebug.Neo3
{
    using StackItem = Neo.VM.Types.StackItem;
    using StackItemType = Neo.VM.Types.StackItemType;

    static class Extensions
    {
        public static bool StartsWith<T>(this ReadOnlyMemory<T> @this, ReadOnlySpan<T> value)
            where T : IEquatable<T>
        {
            return @this.Length >= value.Length 
                && @this.Slice(0, value.Length).Span.SequenceEqual(value);
        }

        public static DebugInfo.Method? GetMethod(this DebugInfo debugInfo, int instructionPointer)
        {
            return debugInfo.Methods
                .SingleOrDefault(m => m.Range.Start <= instructionPointer && instructionPointer <= m.Range.End);
        }

        public static bool TryGetMethod(this DebugInfo debugInfo, int instructionPointer, [MaybeNullWhen(false)] out DebugInfo.Method method)
        {
            method = debugInfo.GetMethod(instructionPointer);
            return method != null;
        }

        public static string? GetDocumentPath(this DebugInfo.SequencePoint? @this, DebugInfo? debugInfo)
        {
            if (@this != null && debugInfo != null && @this.Document < debugInfo.Documents.Count)
            {
                return debugInfo.Documents[@this.Document];
            }

            return null;
        }

        public static bool PathEquals(this DebugInfo.SequencePoint? @this, DebugInfo? debugInfo, string path)
        {
            return string.Equals(@this.GetDocumentPath(debugInfo), path, StringComparison.InvariantCultureIgnoreCase);
        }

        public static DebugInfo.SequencePoint? GetCurrentSequencePoint(this DebugInfo.Method? method, int instructionPointer)
        {
            if (method != null)
            {
                var sequencePoints = method.SequencePoints.OrderBy(sp => sp.Address).ToArray();
                if (sequencePoints.Length > 0)
                {
                    for (int i = 0; i < sequencePoints.Length; i++)
                    {
                        if (instructionPointer == sequencePoints[i].Address)
                            return sequencePoints[i];
                    }

                    if (instructionPointer <= sequencePoints[0].Address)
                        return sequencePoints[0];

                    for (int i = 0; i < sequencePoints.Length - 1; i++)
                    {
                        if (instructionPointer > sequencePoints[i].Address && instructionPointer <= sequencePoints[i + 1].Address)
                            return sequencePoints[i];
                    }
                }
            }

            return null;
        }


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

        public static JToken ToJson(this StackItem item)
        {
            return item switch
            {
                Neo.VM.Types.Boolean _ => item.ToBoolean(),
                // Neo.VM.Types.Buffer buffer => "Buffer",
                Neo.VM.Types.ByteString byteString => byteString.Span.ToHexString(), 
                Neo.VM.Types.Integer @int => @int.ToBigInteger().ToString(),
                // Neo.VM.Types.InteropInterface _ => MakeVariable("InteropInterface"),
                // Neo.VM.Types.Map _ => MakeVariable("Map"),
                Neo.VM.Types.Null _ => new JValue((object?)null),
                // Neo.VM.Types.Pointer _ => MakeVariable("Pointer"),
                Neo.VM.Types.Array array => new JArray(array.Select(i => i.ToJson())),
                _ => throw new NotImplementedException(),
            };

        }

        public static string ToResult(this StackItem item)
        {
            return item.ToJson().ToString(Formatting.Indented);
        }

        public static Variable ForEvaluation(this Variable @this, string prefix = "")
        {
            @this.EvaluateName = prefix + @this.Name;
            return @this;
        }        

        public static EvaluateResponse ToEvaluateResponse(this Variable @this)
        {
            return new EvaluateResponse(@this.Value, @this.VariablesReference);
        }

        public static Variable ToVariable(this byte[] item, IVariableManager manager, string name, string? typeHint = null)
        {
            return ToVariable((StackItem)item, manager, name, typeHint);
        }

        public static Variable ToVariable(this bool item, IVariableManager manager, string name, string? typeHint = null)
        {
            return ToVariable((StackItem)item, manager, name, typeHint);
        }

        public static Variable ToVariable(this StackItem item, IVariableManager manager, string name, string? typeHint = null)
        {
            switch (typeHint)
            {
                case "Boolean":
                    return MakeVariable(item.ToBoolean().ToString(), "Boolean");
                case "Integer":
                {
                    var value = item.IsNull ? "0" : 
                        ((Neo.VM.Types.Integer)item.ConvertTo(StackItemType.Integer)).ToBigInteger().ToString();
                    return MakeVariable(value, "Integer");
                }
                case "String":
                {
                    var value = item.IsNull ? "<null>" : 
                        Encoding.UTF8.GetString(((Neo.VM.Types.ByteString)item.ConvertTo(StackItemType.ByteString)).Span);
                    return MakeVariable(value, "String");
                }
                case "HexString":
                {
                    var value = item.IsNull ? "<null>" : 
                        ((Neo.VM.Types.ByteString)item.ConvertTo(StackItemType.ByteString)).Span.ToHexString();
                    return MakeVariable(value, "HexString");
                }
                case "ByteArray":
                {
                    if (item.IsNull)
                    {
                        return MakeVariable("<null>", "ByteArray");
                    }
                    var byteString = (Neo.VM.Types.ByteString)item.ConvertTo(StackItemType.ByteString);
                    return ByteArrayContainer.Create(manager, byteString, name);
                }
            }

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
