using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo.VM;
using Neo.VM.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

#nullable enable

namespace NeoDebug.Adapter
{
    using VMArray = Neo.VM.Types.Array;
    using VMBoolean = Neo.VM.Types.Boolean;

    internal enum StackItemType : byte
    {
        ByteArray = 0x00,
        Boolean = 0x01,
        Integer = 0x02,
        InteropInterface = 0x40,
        Array = 0x80,
        Struct = 0x81,
        Map = 0x82,
    }

    internal partial class InteropService
    {
        private readonly TriggerType trigger = TriggerType.Application;
        private readonly bool checkWitnessBypass = false;
        private readonly bool checkWitnessBypassValue;
        private readonly IEnumerable<byte[]> witnesses = Enumerable.Empty<byte[]>();

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
            throw new NotImplementedException(nameof(Runtime_Platform));
        }

        private bool Runtime_Deserialize(ExecutionEngine engine)
        {
            throw new NotImplementedException(nameof(Runtime_Deserialize));
        }

        // TODO: rewrite SerializeStackItem
        private void SerializeStackItem(StackItem item, System.IO.BinaryWriter writer)
        {
            List<StackItem> serialized = new List<StackItem>();
            Stack<StackItem> unserialized = new Stack<StackItem>();
            unserialized.Push(item);
            while (unserialized.Count > 0)
            {
                item = unserialized.Pop();
                switch (item)
                {
                    case ByteArray _:
                        writer.Write((byte)StackItemType.ByteArray);
                        writer.WriteVarBytes(item.GetByteArray());
                        break;
                    case VMBoolean _:
                        writer.Write((byte)StackItemType.Boolean);
                        writer.Write(item.GetBoolean());
                        break;
                    case Integer _:
                        writer.Write((byte)StackItemType.Integer);
                        writer.WriteVarBytes(item.GetByteArray());
                        break;
                    case InteropInterface _:
                        throw new NotSupportedException();
                    case VMArray array:
                        if (serialized.Any(p => ReferenceEquals(p, array)))
                            throw new NotSupportedException();
                        serialized.Add(array);
                        if (array is Struct)
                            writer.Write((byte)StackItemType.Struct);
                        else
                            writer.Write((byte)StackItemType.Array);
                        writer.WriteVarInt(array.Count);
                        for (int i = array.Count - 1; i >= 0; i--)
                            unserialized.Push(array[i]);
                        break;
                    case Map map:
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

        private bool Runtime_Serialize(ExecutionEngine engine)
        {
            using (var ms = new System.IO.MemoryStream())
            using (var writer = new System.IO.BinaryWriter(ms))
            {
                try
                {
                    SerializeStackItem(engine.CurrentContext.EvaluationStack.Pop(), writer);
                }
                catch (NotSupportedException)
                {
                    return false;
                }
                writer.Flush();
                if (ms.Length > engine.MaxItemSize)
                    return false;
                engine.CurrentContext.EvaluationStack.Push(ms.ToArray());
            }
            return true;
        }

        private bool Runtime_GetTime(ExecutionEngine engine)
        {
            throw new NotImplementedException(nameof(Runtime_GetTime));
        }

        private bool Runtime_Log(ExecutionEngine engine)
        {
            throw new NotImplementedException(nameof(Runtime_Log));
        }

        private bool Runtime_Notify(ExecutionEngine engine)
        {
            var state = engine.CurrentContext.EvaluationStack.Pop() as VMArray;
            if (state != null)
            {
                var name = Encoding.UTF8.GetString(state[0].GetByteArray());
                var @event = new OutputEvent()
                {
                    Output = $"Notify: {name}\n",
                };
                sendOutput(@event);
            }
            return true;
        }

        private bool Runtime_CheckWitness(ExecutionEngine engine)
        {
            var evalStack = engine.CurrentContext.EvaluationStack;
            var hash = evalStack.Pop().GetByteArray();

            if (checkWitnessBypass)
            {
                evalStack.Push(checkWitnessBypassValue);
                return true;
            }
            else
            {
                var hashSpan = hash.AsSpan();
                foreach (var witness in witnesses)
                {
                    if (hashSpan.SequenceEqual(witness))
                    {
                        evalStack.Push(true);
                        return true;
                    }
                }

                evalStack.Push(false);
                return true;
            }
        }

        private bool Runtime_GetTrigger(ExecutionEngine engine)
        {
            engine.CurrentContext.EvaluationStack.Push((int)trigger);
            return true;
        }
    }
}
