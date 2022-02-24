using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo;
using Neo.BlockchainToolkit;
using Neo.BlockchainToolkit.Models;
using Neo.Cryptography.ECC;
using Neo.SmartContract;
using Newtonsoft.Json.Linq;
using OneOf;

namespace NeoDebug.Neo3
{
    using StackItem = Neo.VM.Types.StackItem;
    using StackItemType = Neo.VM.Types.StackItemType;
    using ByteString = Neo.VM.Types.ByteString;

    internal static class Extensions
    {
        public static ReadOnlyMemory<byte> AsReadOnlyMemory(this StackItem item)
        {
            if (item is Neo.VM.Types.Buffer buffer) return buffer.InnerBuffer;
            if (item is Neo.VM.Types.ByteString byteString) return byteString;
            if (item is Neo.VM.Types.PrimitiveType) return (Neo.VM.Types.ByteString)item.ConvertTo(StackItemType.ByteString);
            
            throw new InvalidOperationException($"{item.Type} not accessable as ReadOnlyMemory<byte>");
        }


        public static bool StartsWith<T>(this ReadOnlyMemory<T> @this, ReadOnlySpan<T> value)
            where T : IEquatable<T>
        {
            return @this.Span.StartsWith(value);
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
            if (@this != null && debugInfo != null
                && @this.Document >= 0
                && @this.Document < debugInfo.Documents.Count)
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

        public static JToken ToJson(this StackItem item)
        {
            return item switch
            {
                Neo.VM.Types.Boolean _ => item.GetBoolean(),
                Neo.VM.Types.Buffer buffer => buffer.GetSpan().ToHexString(),
                Neo.VM.Types.ByteString byteString => byteString.GetSpan().ToHexString(),
                Neo.VM.Types.Integer @int => @int.GetInteger().ToString(),
                // Neo.VM.Types.InteropInterface _ => MakeVariable("InteropInterface"),
                Neo.VM.Types.Map map => MapToJson(map),
                Neo.VM.Types.Null _ => new JValue((object?)null),
                // Neo.VM.Types.Pointer _ => MakeVariable("Pointer"),
                Neo.VM.Types.Array array => new JArray(array.Select(i => i.ToJson())),
                _ => throw new NotSupportedException(),
            };

            static JObject MapToJson(Neo.VM.Types.Map map)
            {
                var json = new JObject();
                foreach (var (key, value) in map)
                {
                    json.Add(PrimitiveTypeToString(key), value.ToJson());
                }
                return json;
            }

            static string PrimitiveTypeToString(Neo.VM.Types.PrimitiveType item)
            {
                try
                {
                    return item.GetString() ?? throw new Exception();
                }
                catch
                {
                    return Convert.ToHexString(item.GetSpan());
                }
            }
        }

        public static bool TryGetInteropType(this Neo.VM.Types.InteropInterface item, [MaybeNullWhen(false)] out Type type)
        {
            var iiType = typeof(Neo.VM.Types.InteropInterface);
            var field = iiType.GetField("_object", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null)
            {
                type = field.FieldType;
                return true;
            }

            type = default;
            return false;
        }
    }
}
