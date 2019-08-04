using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo.VM;
using System;
using System.Linq;

namespace Neo.DebugAdapter
{
    static class Extensions
    {
        public static Method GetEntryPoint(this Contract contract) => contract.DebugInfo.Methods.Single(m => m.Name == contract.DebugInfo.Entrypoint);

        public static Method GetMethod(this Contract contract, ExecutionContext context)
        {
            if (contract.ScriptHash.AsSpan().SequenceEqual(context.ScriptHash))
            {
                var ip = context.InstructionPointer;
                return contract.DebugInfo.Methods
                    .SingleOrDefault(m => m.StartAddress <= ip && ip <= m.EndAddress);
            }

            return null;
        }

        public static SequencePoint GetCurrentSequencePoint(this Method method, Neo.VM.ExecutionContext context)
        {
            return method?.SequencePoints.SingleOrDefault(sp => sp.Address == context.InstructionPointer);
        }

        public static SequencePoint GetNextSequencePoint(this Method method, Neo.VM.ExecutionContext context)
        {
            return method?.SequencePoints
                .OrderBy(sp => sp.Address)
                .FirstOrDefault(sp => sp.Address > context.InstructionPointer);
        }

        public static Variable GetVariable(this StackItem item, NeoDebugSession session, Parameter parameter = null)
        {
            if (parameter != null)
            {
                switch (parameter.Type)
                {
                    case "Integer":
                        return new Variable()
                        {
                            Name = parameter.Name,
                            Value = item.GetBigInteger().ToString(),
                            Type = "Integer"
                        };
                    case "String":
                        return new Variable()
                        {
                            Name = parameter.Name,
                            Value = item.GetString(),
                            Type = "String"
                        };
                }
            }

            switch (item)
            {
                case Neo.VM.Types.Boolean _:
                    return new Variable()
                    {
                        Name = parameter?.Name,
                        Value = item.GetBoolean().ToString(),
                        Type = "Boolean"
                    };
                case Neo.VM.Types.Integer _:
                    return new Variable()
                    {
                        Name = parameter?.Name,
                        Value = item.GetBigInteger().ToString(),
                        Type = "Integer"
                    };
                case Neo.VM.Types.InteropInterface _:
                    return new Variable()
                    {
                        Name = parameter?.Name,
                        Value = "<interop interface>"
                    };
                case Neo.VM.Types.Struct _: // struct before array
                case Neo.VM.Types.Map _:
                    return new Variable()
                    {
                        Name = parameter?.Name,
                        Value = item.GetType().Name
                    };
                case Neo.VM.Types.ByteArray byteArray:
                    {
                        var container = new ByteArrayContainer(session, byteArray);
                        var containerID = session.AddVariableContainer(container);
                        return new Variable()
                        {
                            Name = parameter?.Name,
                            Type = $"ByteArray[{byteArray.GetByteArray().Length}]>",
                            VariablesReference = containerID,
                            IndexedVariables = byteArray.GetByteArray().Length,
                            NamedVariables = 1
                        };
                    }
                case Neo.VM.Types.Array array:
                    {
                        var container = new ArrayContainer(session, array);
                        var containerID = session.AddVariableContainer(container);
                        return new Variable()
                        {
                            Name = parameter?.Name,
                            Type = $"Array[{array.Count}]",
                            VariablesReference = containerID,
                            IndexedVariables = array.Count,
                        };
                    }
                default:
                    throw new NotImplementedException($"GetStackItemValue {item.GetType().FullName}");
            }
        }

        public static string GetStackItemValue(this StackItem item, string type)
        {
            switch (type)
            {
                case "Integer":
                    return item.GetBigInteger().ToString();
                case "String":
                    return item.GetString();
                case "ByteArray":
                    return GetStackItemValue(item);
                case "Array":
                    {
                        if (!(item is Neo.VM.Types.Array))
                            throw new ArgumentException();

                        return GetStackItemValue(item);
                    }
                default:
                    throw new NotImplementedException($"GetStackItemValue {type}");
            }
        }

        public static string GetStackItemValue(this StackItem item)
        {
            switch (item)
            {
                case Neo.VM.Types.Boolean _:
                    return item.GetBoolean().ToString();
                case Neo.VM.Types.Integer _:
                    return item.GetBigInteger().ToString();
                case Neo.VM.Types.ByteArray _array:
                    {
                        var array = _array.GetByteArray();
                        var builder = new System.Text.StringBuilder();
                        builder.Append("{");
                        var first = true;
                        for (int i = 0; i < array.Length; i++)
                        {
                            if (first)
                            {
                                first = false;
                            }
                            else
                            {
                                builder.Append(", ");
                            }
                            builder.Append(array[i].ToString("X"));
                        }
                        builder.Append("}");
                        return builder.ToString();
                    }
                case Neo.VM.Types.Array array:
                    {
                        var builder = new System.Text.StringBuilder();
                        var first = true;
                        builder.Append("[");
                        for (int i = 0; i < array.Count; i++)
                        {
                            if (first)
                            {
                                first = false;
                            }
                            else
                            {
                                builder.Append(", ");
                            }

                            builder.Append(GetStackItemValue(array[i]));
                        }
                        builder.Append("]");
                        return builder.ToString();
                    }
                case VM.Types.InteropInterface _:
                    // TODO: enhance VM so debugger can get more info about InteropInterface instances
                    return "<InteropInterface>";
                default:
                    throw new NotImplementedException($"GetStackItemValue {item.GetType().FullName}");
            }
        }
    }
}
