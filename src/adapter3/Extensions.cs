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
        public static IReadOnlyList<T> AsReadOnly<T>(this T[] @this)
            => @this;

        public static IReadOnlyDictionary<TKey, TSource> AsReadOnly<TKey, TSource>(this IDictionary<TKey, TSource> source)
            => (IReadOnlyDictionary<TKey, TSource>)source;

        public static ReadOnlyMemory<byte> AsReadOnlyMemory(this StackItem item)
        {
            if (item is Neo.VM.Types.Buffer buffer) return buffer.InnerBuffer;
            if (item is Neo.VM.Types.ByteString byteString) return byteString;
            if (item is Neo.VM.Types.PrimitiveType) return (Neo.VM.Types.ByteString)item.ConvertTo(StackItemType.ByteString);

            throw new InvalidOperationException($"{item.Type} not accessable as ReadOnlyMemory<byte>");
        }

        public static bool TryGetMethod(this DebugInfo debugInfo, int instructionPointer, out DebugInfo.Method method)
        {
            for (int i = 0; i < debugInfo.Methods.Count; i++)
            {
                var range = debugInfo.Methods[i].Range;
                if (range.Start <= instructionPointer && instructionPointer <= range.End)
                {
                    method = debugInfo.Methods[i];
                    return true;
                }
            }

            method = default;
            return false;
        }

        public static bool TryGetSequencePoint(this DebugInfo.Method method, int instructionPointer, out DebugInfo.SequencePoint sequencePoint)
        {
            var points = method.SequencePoints ?? Array.Empty<DebugInfo.SequencePoint>();

            for (int i = points.Count - 1; i >= 0; i--)
            {
                if (instructionPointer >= points[i].Address)
                {
                    sequencePoint = points[i];
                    return true;
                }
            }

            if (points.Count > 0)
            {
                sequencePoint = points[0];
                return true;
            }

            sequencePoint = default;
            return false;
        }

        public static bool TryGetDocumentPath(this DebugInfo.SequencePoint @this, DebugInfo? debugInfo, out string path)
        {
            var docs = debugInfo?.Documents ?? Array.Empty<string>();
            if (@this.Document >= 0 && @this.Document < docs.Count)
            {
                path = docs[@this.Document];
                return true;
            }

            path = string.Empty;
            return false;
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

        public static EvaluateResponse AsFailedEval(this EvaluateResponse @this)
        {
            throw new Exception(@this.Result);

            // @this.PresentationHint = new VariablePresentationHint
            // {
            //     Attributes = VariablePresentationHint.AttributesValue.FailedEvaluation
            // };
            // return @this;
        }

        public static ReadOnlyMemory<byte> AsMemory(this StackItem item)
            => item.IsNull
                ? default
                : item is Neo.VM.Types.Buffer buffer
                    ? buffer.InnerBuffer.AsMemory()
                    : item is Neo.VM.Types.ByteString byteString
                        ? byteString
                        : item is Neo.VM.Types.PrimitiveType primitive
                            ? primitive.GetSpan().ToArray()
                            : throw new InvalidCastException($"Cannot get memory of {item.Type}");

        public static ReadOnlySpan<byte> AsSpan(this StackItem item)
            => item.IsNull
                ? default
                : item is Neo.VM.Types.PrimitiveType primitive
                    ? primitive.GetSpan()
                    : item is Neo.VM.Types.Buffer buffer
                        ? buffer.InnerBuffer.AsSpan()
                        : throw new InvalidCastException($"Cannot get span of {item.Type}");


    }
}
