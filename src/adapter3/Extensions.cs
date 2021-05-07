using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo;
using Neo.BlockchainToolkit.Models;
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
                    json.Add(key.GetString() ?? throw new Exception(), value.ToJson());
                }
                return json;
            }
        }

        public static string? TryConvert(this StackItem item, string typeHint)
        {
            try
            {
                return typeHint switch
                {
                    "Boolean" => item.GetBoolean().ToString(),
                    "Integer" => item.IsNull
                        ? BigInteger.Zero.ToString()
                        : ((Neo.VM.Types.Integer)item.ConvertTo(StackItemType.Integer)).GetInteger().ToString(),
                    "String" => item.GetString(),
                    "HexString" => ToHexString(item),
                    "ByteArray" => ToHexString(item),
                    _ => null
                };
            }
            catch
            {
                return null;
            }

            static string ToHexString(StackItem item) => item.IsNull
                ? "<null>"
                : ((ByteString)item.ConvertTo(StackItemType.ByteString)).GetSpan().ToHexString();
        }

        public static Variable ToVariable(this StackItem item, IVariableManager manager, string name, string typeHint = "")
        {
            if (typeHint == "ByteArray")
            {
                if (item.IsNull) return new Variable { Name = name, Value = "<null>", Type = typeHint };

                try
                {
                    var byteString = (ByteString)item.ConvertTo(StackItemType.ByteString);
                    return ByteArrayContainer.Create(manager, byteString, name);
                }
                catch
                {
                    // if ConvertTo ByteString fails, continue into non type-hinted variable resolution
                }
            }
            else
            {
                var value = item.TryConvert(typeHint);
                if (value != null)
                {
                    return new Variable { Name = name, Value = value, Type = typeHint };
                }
            }

            return item switch
            {
                Neo.VM.Types.Boolean _ => new Variable { Name = name, Value = $"{item.GetBoolean()}", Type = "Boolean" },
                Neo.VM.Types.Buffer buffer => ByteArrayContainer.Create(manager, buffer, name),
                Neo.VM.Types.ByteString byteString => ByteArrayContainer.Create(manager, byteString, name),
                Neo.VM.Types.Integer @int => new Variable { Name = name, Value = $"{@int.GetInteger()}", Type = "Boolean" },
                Neo.VM.Types.InteropInterface _ => new Variable { Name = name, Value = "InteropInterface" },
                Neo.VM.Types.Map map => NeoMapContainer.Create(manager, map, name),
                Neo.VM.Types.Null _ => new Variable { Name = name, Value = "<null>", Type = "Null" },
                Neo.VM.Types.Pointer _ => new Variable { Name = name, Value = "Pointer" },
                Neo.VM.Types.Array array => NeoArrayContainer.Create(manager, array, name),
                _ => throw new NotSupportedException(),
            };
        }
    }
}
