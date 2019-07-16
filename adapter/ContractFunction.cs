using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo.VM;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace Neo.DebugAdapter
{

    internal class ContractFunction
    {
        public string Name;
        public (string Name, ContractParameterType ParamType)[] Parameters;
        public ContractParameterType ReturnType;

        public static ContractParameterType ParseType(JToken jtoken) => Enum.Parse<ContractParameterType>(jtoken.Value<string>());

        public static ContractFunction FromJson(JToken json)
        {
            var name = json.Value<string>("name");
            var retType = ParseType(json["returntype"]);
            var @params = json["parameters"].Select(j => (j.Value<string>("name"), ParseType(j["type"])));

            return new ContractFunction
            {
                Name = name,
                Parameters = @params.ToArray(),
                ReturnType = retType
            };
        }
    }
}
