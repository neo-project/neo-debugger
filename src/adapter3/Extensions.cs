using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using System.Text;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo;
using Neo.BlockchainToolkit.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NeoDebug.Neo3
{
    using StackItem = Neo.VM.Types.StackItem;
    using StackItemType = Neo.VM.Types.StackItemType;
    using ByteString = Neo.VM.Types.ByteString;

    internal static class Extensions
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
            return string.Equals(@this.GetDocumentPath(debugInfo), path, StringComparison.OrdinalIgnoreCase);
        }

        public static DebugInfo.SequencePoint GetCurrentSequencePoint(this DebugInfo.Method method, int instructionPointer)
        {
            var sequencePoints = method.SequencePoints;
            if (sequencePoints.Count == 0)
            {
                throw new InvalidOperationException($"{method.Name} has no sequence points");
            }

            for (int i = sequencePoints.Count - 1; i >= 0; i--)
            {
                if (instructionPointer >= sequencePoints[i].Address)
                    return sequencePoints[i];
            }

            return sequencePoints[0];
        }

        //https://stackoverflow.com/a/1646913
        public static int GetSequenceHashCode(this ReadOnlySpan<byte> span)
        {
            unchecked
            {
                int hash = 17;
                for (int i = 0; i < span.Length; i++)
                {
                    hash = (hash * 31) + span[i];
                }
                return hash;
            }
        }

        public static int GetSequenceHashCode(this byte[] array)
        {
            return GetSequenceHashCode(array.AsSpan());
        }

        public static JToken ToJson(this StackItem item, string typeHint = "")
        {
            var stringRep = item.ToStringRep(typeHint);

            return stringRep != null
                ? (JToken)stringRep
                : item switch
                {
                    Neo.VM.Types.Boolean _ => item.GetBoolean(),
                    // Neo.VM.Types.Buffer buffer => "Buffer",
                    Neo.VM.Types.ByteString byteString => byteString.GetSpan().ToHexString(),
                    Neo.VM.Types.Integer @int => @int.GetInteger().ToString(),
                    // Neo.VM.Types.InteropInterface _ => MakeVariable("InteropInterface"),
                    // Neo.VM.Types.Map _ => MakeVariable("Map"),
                    Neo.VM.Types.Null _ => new JValue((object?)null),
                    // Neo.VM.Types.Pointer _ => MakeVariable("Pointer"),
                    Neo.VM.Types.Array array => new JArray(array.Select(i => i.ToJson())),
                    _ => throw new NotSupportedException(),
                };
        }

        public static string ToResult(this StackItem item, string typeHint = "")
        {
            return item.ToJson(typeHint).ToString(Formatting.Indented);
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

        public static Variable ToVariable(this byte[] item, IVariableManager manager, string name, string typeHint = "")
        {
            return ToVariable((StackItem)item, manager, name, typeHint);
        }

        public static Variable ToVariable(this ReadOnlyMemory<byte> item, IVariableManager manager, string name, string typeHint = "")
        {
            return ToVariable((StackItem)item, manager, name, typeHint);
        }

        public static Variable ToVariable(this bool item, IVariableManager manager, string name, string typeHint = "")
        {
            return ToVariable((StackItem)item, manager, name, typeHint);
        }

        public static string ToStrictUTF8String(this byte[] @this) => Neo.Utility.StrictUTF8.GetString(@this);

        public static string ToStrictUTF8String(this StackItem item) => item.IsNull
            ? "<null>"
            : Neo.Utility.StrictUTF8.GetString(((ByteString)item.ConvertTo(StackItemType.ByteString)).GetSpan());

        public static string ToHexString(this StackItem item) => item.IsNull
            ? "<null>"
            : ((ByteString)item.ConvertTo(StackItemType.ByteString)).GetSpan().ToHexString();

        public static BigInteger ToBigInteger(this StackItem item) => item.IsNull
            ? BigInteger.Zero
            : ((Neo.VM.Types.Integer)item.ConvertTo(StackItemType.Integer)).GetInteger();

        private static string? ToStringRep(this StackItem item, string typeHint = "")
        {
            return typeHint switch
            {
                "Boolean" => item.GetBoolean().ToString(),
                "Integer" => item.ToBigInteger().ToString(),
                "String" => item.ToStrictUTF8String(),
                "HexString" => item.ToHexString(),
                "ByteArray" => item.ToHexString(),
                _ => null
            };
        }

        public static Variable ToVariable(this StackItem item, IVariableManager manager, string name, string typeHint = "")
        {
            if (typeHint == "ByteArray")
            {
                if (item.IsNull)
                {
                    return MakeVariable("<null>", "ByteArray");
                }

                var byteString = (ByteString)item.ConvertTo(StackItemType.ByteString);
                return ByteArrayContainer.Create(manager, byteString, name);
            }

            var stringRep = item.ToStringRep(typeHint);
            if (stringRep != null)
            {
                return MakeVariable(stringRep, typeHint);
            }

            return item switch
            {
                Neo.VM.Types.Boolean _ => MakeVariable($"{item.GetBoolean()}", "Boolean"),
                Neo.VM.Types.Buffer buffer => ByteArrayContainer.Create(manager, buffer, name),
                Neo.VM.Types.ByteString byteString => ByteArrayContainer.Create(manager, byteString, name),
                Neo.VM.Types.Integer @int => MakeVariable($"{@int.GetInteger()}", "Integer"),
                Neo.VM.Types.InteropInterface _ => MakeVariable("InteropInterface"),
                Neo.VM.Types.Map _ => MakeVariable("Map"),
                Neo.VM.Types.Null _ => MakeVariable("null", "Null"),
                Neo.VM.Types.Pointer _ => MakeVariable("Pointer"),
                Neo.VM.Types.Array array => NeoArrayContainer.Create(manager, array, name),
                _ => throw new NotSupportedException(),
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
