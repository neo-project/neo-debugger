using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo.VM;
using NeoDebug;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NeoDebug
{
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
            engine.CurrentContext.EvaluationStack.Push(Encoding.ASCII.GetBytes("NEO"));
            return true;
        }

        private bool Runtime_Serialize(ExecutionEngine engine)
        {
            var evalStack = engine.CurrentContext.EvaluationStack;
            var item = evalStack.Pop();
            if (SerializationHelpers.TrySerialize(item, engine.MaxItemSize, out var array))
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
            if (SerializationHelpers.TryDeserialize(data, engine, out var item))
            {
                evalStack.Push(item);
                return true;
            }

            return false;
        }

        private bool Runtime_Log(ExecutionEngine engine)
        {
            string message = Encoding.UTF8.GetString(engine.CurrentContext.EvaluationStack.Pop().GetByteArray());
            sendOutput(new OutputEvent()
            {
                Output = $"Runtime.Log: {message}\n",
            });
            return true;
        }

        private bool Runtime_Notify(ExecutionEngine engine)
        {
            static string StackItemToString(StackItem item, string? typeHint = null)
            {
                return typeHint switch
                {
                    "Boolean" => item.GetBoolean().ToString(),
                    "Integer" => item.GetBigInteger().ToString(),
                    "String" => item.GetString(),
                    _ => item.GetBigInteger().ToHexString(),
                };
            }

            if (engine.CurrentContext.EvaluationStack.Pop() is Neo.VM.Types.Array state && state.Count >= 1)
            {
                var name = Encoding.UTF8.GetString(state[0].GetByteArray());
                var paramTypes = contract.GetEvent(name)?.Parameters ?? new List<(string name, string type)>();
                var @params = new Newtonsoft.Json.Linq.JArray();
                for (int i = 1; i < state.Count; i++)
                {
                    var paramType = i <= paramTypes.Count ? paramTypes[i - 1].Type : string.Empty;
                    @params.Add(StackItemToString(state[i], paramType));
                }

                sendOutput(new OutputEvent()
                {
                    Output = $"Runtime.Notify: {name} {@params.ToString(Newtonsoft.Json.Formatting.None)}\n",
                });
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
    }
}
