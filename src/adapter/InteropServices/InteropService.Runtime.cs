using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo.VM;
using NeoDebug.Models;
using NeoFx;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;

namespace NeoDebug
{
    partial class InteropService
    {
        private void RegisterRuntime(Action<string, Func<ExecutionEngine, bool>, int> register)
        {
            register("Neo.Runtime.GetTrigger", Runtime_GetTrigger, 1);
            register("Neo.Runtime.CheckWitness", Runtime_CheckWitness, 200);
            register("Neo.Runtime.Notify", Runtime_Notify, 1);
            register("Neo.Runtime.Log", Runtime_Log, 1);
            register("Neo.Runtime.GetTime", Runtime_GetTime, 1);
            register("Neo.Runtime.Serialize", Runtime_Serialize, 1);
            register("Neo.Runtime.Deserialize", Runtime_Deserialize, 1);

            register("System.Runtime.Platform", Runtime_Platform, 1);
            register("System.Runtime.GetTrigger", Runtime_GetTrigger, 1);
            register("System.Runtime.CheckWitness", Runtime_CheckWitness, 200);
            register("System.Runtime.Notify", Runtime_Notify, 1);
            register("System.Runtime.Log", Runtime_Log, 1);
            register("System.Runtime.GetTime", Runtime_GetTime, 1);
            register("System.Runtime.Serialize", Runtime_Serialize, 1);
            register("System.Runtime.Deserialize", Runtime_Deserialize, 1);

            register("AntShares.Runtime.CheckWitness", Runtime_CheckWitness, 200);
            register("AntShares.Runtime.Notify", Runtime_Notify, 1);
            register("AntShares.Runtime.Log", Runtime_Log, 1);
        }

        private bool Runtime_Platform(ExecutionEngine engine)
        {
            engine.CurrentContext.EvaluationStack.Push(Encoding.ASCII.GetBytes("NEO"));
            return true;
        }

        private bool Runtime_Serialize(ExecutionEngine engine)
        {
            var evalStack = engine.CurrentContext.EvaluationStack;
            var item = evalStack.Pop();
            if (TrySerialize(item, engine.MaxItemSize, out var array))
            {
                evalStack.Push(array);
                return true;
            }

            return false;
        }

        private bool Runtime_Deserialize(ExecutionEngine engine)
        {
            var evalStack = engine.CurrentContext.EvaluationStack;
            var data = evalStack.Pop().GetByteArray();
            if (TryDeserialize(data, engine, out var item))
            {
                evalStack.Push(item);
                return true;
            }

            return false;
        }

        private bool Runtime_Log(ExecutionEngine engine)
        {
            var scriptHash = new UInt160(engine.CurrentContext.ScriptHash);
            var message = Encoding.UTF8.GetString(engine.CurrentContext.EvaluationStack.Pop().GetByteArray());
            sendOutput(new OutputEvent()
            {
                Output = $"Runtime.Log: {scriptHash} {message}\n",
            });
            return true;
        }

        private bool Runtime_Notify(ExecutionEngine engine)
        {
            var output = GetOutput(new UInt160(engine.CurrentContext.ScriptHash),
                                   engine.CurrentContext.EvaluationStack.Pop());
            sendOutput(new OutputEvent()
            {
                Output = output,
            });
            return true;

            string GetOutput(UInt160 scriptHash, StackItem state)
            {
                if (state is Neo.VM.Types.Array array && array.Count >= 1)
                {
                    var name = Encoding.UTF8.GetString(array[0].GetByteArray());
                    if (events.TryGetValue((scriptHash, name), out var @event))
                    {
                        var @params = new JArray();
                        for (int i = 1; i < array.Count; i++)
                        {
                            var paramType = @event.Parameters.ElementAtOrDefault(i - 1);
                            @params.Add(StackItemToJson(array[i], paramType.Type));
                        }

                        return $"Runtime.Notify: {scriptHash} {name}\n{@params.ToString(Formatting.Indented)}\n";
                    }
                }

                return $"Runtime.Notify: {scriptHash} \n{StackItemToJson(state, null).ToString(Formatting.Indented)}\n";
            }

            static JToken StackItemToJson(StackItem item, string? typeHint)
            {
                return typeHint switch
                {
                    "Boolean" => item.GetBoolean().ToString(),
                    "Integer" => item.GetBigInteger().ToString(),
                    "String" => item.GetString(),
                    _ => item switch
                    {
                        Neo.VM.Types.Boolean _ => item.GetBoolean().ToString(),
                        Neo.VM.Types.ByteArray _ => $"{item.GetBigInteger().ToHexString()} ({item.GetString()})",
                        Neo.VM.Types.Integer _ => item.GetBigInteger().ToString(),
                        Neo.VM.Types.Array array => new JArray(array.Select(i => StackItemToJson(i, null))),
                        _ => throw new NotSupportedException(item.GetType().Name),
                    }
                };
            }
        }

        private bool Runtime_CheckWitness(ExecutionEngine engine)
        {
            var evalStack = engine.CurrentContext.EvaluationStack;
            var hash = evalStack.Pop().GetByteArray();

            var result = witnessChecker.Check(hash);
            evalStack.Push(result);
            return true;
        }

        private bool Runtime_GetTrigger(ExecutionEngine engine)
        {
            engine.CurrentContext.EvaluationStack.Push((int)trigger);
            return true;
        }

        private bool Runtime_GetTime(ExecutionEngine engine)
        {
            // TODO: enable setting time and/or SecondsPerBlock from config
            const int secondsPerBlock = 15;

            if (blockchain != null
                && blockchain.TryGetCurrentBlockHash(out var hash)
                && blockchain.TryGetBlock(hash, out var header, out var _))
            {
                engine.CurrentContext.EvaluationStack.Push(header.Timestamp.ToUnixTimeSeconds() + secondsPerBlock);
                return true;
            }

            return false;
        }

        // This code is nearly identical to the code from neo code base. As much as I would like to rewrite it 
        // for efficiency, compatibility with existing implementation is higher priority.

        static bool TrySerialize(StackItem stackItem, uint maxItemSize, [NotNullWhen(true)] out byte[]? array)
        {
            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                try
                {
                    SerializeStackItem(stackItem, writer);
                }
                catch (NotSupportedException)
                {
                    array = null!;
                    return false;
                }

                writer.Flush();
                if (ms.Length <= maxItemSize)
                {
                    array = ms.ToArray();
                    return true;
                }
            }

            array = null;
            return false;
        }

        static void SerializeStackItem(StackItem item, BinaryWriter writer)
        {
            List<StackItem> serialized = new List<StackItem>();
            Stack<StackItem> unserialized = new Stack<StackItem>();
            unserialized.Push(item);
            while (unserialized.Count > 0)
            {
                item = unserialized.Pop();
                switch (item)
                {
                    case Neo.VM.Types.ByteArray _:
                        writer.Write((byte)StackItemType.ByteArray);
                        writer.WriteVarBytes(item.GetByteArray());
                        break;
                    case Neo.VM.Types.Boolean _:
                        writer.Write((byte)StackItemType.Boolean);
                        writer.Write(item.GetBoolean());
                        break;
                    case Neo.VM.Types.Integer _:
                        writer.Write((byte)StackItemType.Integer);
                        writer.WriteVarBytes(item.GetByteArray());
                        break;
                    case Neo.VM.Types.InteropInterface _:
                        throw new NotSupportedException();
                    case Neo.VM.Types.Array array:
                        if (serialized.Any(p => ReferenceEquals(p, array)))
                            throw new NotSupportedException();
                        serialized.Add(array);
                        if (array is Neo.VM.Types.Struct)
                            writer.Write((byte)StackItemType.Struct);
                        else
                            writer.Write((byte)StackItemType.Array);
                        writer.WriteVarInt(array.Count);
                        for (int i = array.Count - 1; i >= 0; i--)
                            unserialized.Push(array[i]);
                        break;
                    case Neo.VM.Types.Map map:
                        if (serialized.Any(p => ReferenceEquals(p, map)))
                            throw new NotSupportedException();
                        serialized.Add(map);
                        writer.Write((byte)StackItemType.Map);
                        writer.WriteVarInt(map.Count);
                        foreach (var pair in map.Reverse())
                        {
                            unserialized.Push(pair.Value);
                            unserialized.Push(pair.Key);
                        }
                        break;
                }
            }
        }

        public static bool TryDeserialize(byte[] data, ExecutionEngine engine, [NotNullWhen(true)] out StackItem? item)
        {
            using MemoryStream ms = new MemoryStream(data, false);
            using BinaryReader reader = new BinaryReader(ms);

            try
            {
                item = DeserializeStackItem(reader, engine);
                return true;
            }
            catch (FormatException)
            {
                item = null!;
                return false;
            }
            catch (IOException)
            {
                item = null!;
                return false;
            }
        }

        private static StackItem DeserializeStackItem(BinaryReader reader, ExecutionEngine engine)
        {
            Stack<StackItem> deserialized = new Stack<StackItem>();
            int undeserialized = 1;
            while (undeserialized-- > 0)
            {
                StackItemType type = (StackItemType)reader.ReadByte();
                switch (type)
                {
                    case StackItemType.ByteArray:
                        deserialized.Push(new Neo.VM.Types.ByteArray(reader.ReadVarBytes()));
                        break;
                    case StackItemType.Boolean:
                        deserialized.Push(new Neo.VM.Types.Boolean(reader.ReadBoolean()));
                        break;
                    case StackItemType.Integer:
                        deserialized.Push(new Neo.VM.Types.Integer(new System.Numerics.BigInteger(reader.ReadVarBytes())));
                        break;
                    case StackItemType.Array:
                    case StackItemType.Struct:
                        {
                            int count = (int)reader.ReadVarInt(engine.MaxArraySize);
                            deserialized.Push(new ContainerPlaceholder
                            {
                                Type = type,
                                ElementCount = count
                            });
                            undeserialized += count;
                        }
                        break;
                    case StackItemType.Map:
                        {
                            int count = (int)reader.ReadVarInt(engine.MaxArraySize);
                            deserialized.Push(new ContainerPlaceholder
                            {
                                Type = type,
                                ElementCount = count
                            });
                            undeserialized += count * 2;
                        }
                        break;
                    default:
                        throw new FormatException();
                }
            }
            Stack<StackItem> stack_temp = new Stack<StackItem>();
            while (deserialized.Count > 0)
            {
                StackItem item = deserialized.Pop();
                if (item is ContainerPlaceholder placeholder)
                {
                    switch (placeholder.Type)
                    {
                        case StackItemType.Array:
                            var array = new Neo.VM.Types.Array();
                            for (int i = 0; i < placeholder.ElementCount; i++)
                                array.Add(stack_temp.Pop());
                            item = array;
                            break;
                        case StackItemType.Struct:
                            var @struct = new Neo.VM.Types.Struct();
                            for (int i = 0; i < placeholder.ElementCount; i++)
                                @struct.Add(stack_temp.Pop());
                            item = @struct;
                            break;
                        case StackItemType.Map:
                            var map = new Neo.VM.Types.Map();
                            for (int i = 0; i < placeholder.ElementCount; i++)
                            {
                                StackItem key = stack_temp.Pop();
                                StackItem value = stack_temp.Pop();
                                map.Add(key, value);
                            }
                            item = map;
                            break;
                    }
                }
                stack_temp.Push(item);
            }
            return stack_temp.Peek();
        }

        class ContainerPlaceholder : StackItem
        {
            public StackItemType Type;
            public int ElementCount;

            public override bool Equals(StackItem other) => throw new NotSupportedException();

            public override bool GetBoolean() => throw new NotImplementedException();

            public override byte[] GetByteArray() => throw new NotSupportedException();
        }
    }
}
