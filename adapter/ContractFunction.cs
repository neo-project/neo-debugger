using Neo.VM;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Neo.DebugAdapter
{
    class ContractFunction
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

        public IEnumerable<ContractParameter> ParseArguments(JToken args)
        {
            return args.Select(j => j.Value<string>())
                .Zip(Parameters, (a, p) => ContractParameter.FromArgument(p.ParamType, a));
        }
    }
}
