using Neo.Network.P2P.Payloads;
using Newtonsoft.Json.Linq;
using System;

namespace NeoDebug.Neo3
{
    partial class LaunchConfigParser
    {
        public struct InvokeFileInvocation
        {
            public readonly string Path;

            public InvokeFileInvocation(string path)
            {
                Path = path;
            }

            public static bool TryFromJson(JToken token, out InvokeFileInvocation invocation)
            {
                if (token.Type == JTokenType.Object && token["invoke-file"] != null)
                {
                    invocation = new InvokeFileInvocation(token.Value<string>("invoke-file") ?? "");
                    return true;
                }

                invocation = default;
                return false;
            }
        }

        public struct ContractDeployInvocation
        {
            public static bool TryFromJson(JToken token, out ContractDeployInvocation invocation)
            {
                if (token.Type == JTokenType.String && token.Value<string>() == "deploy")
                {
                    invocation = new ContractDeployInvocation();
                    return true;
                }

                invocation = default;
                return false;
            }
        }

        public struct LaunchInvocation
        {
            public readonly string Contract;
            public readonly string Operation;
            public readonly JArray Args;

            public LaunchInvocation(string contract, string operation, JArray args)
            {
                Contract = contract;
                Operation = operation;
                Args = args;
            }

            public static bool TryFromJson(JToken token, out LaunchInvocation invocation)
            {
                if (token.Type != JTokenType.Object)
                {
                    invocation = default;
                    return false;
                }

                var operation = token.Value<string>("operation");
                if (string.IsNullOrEmpty(operation))
                {
                    invocation = default;
                    return false;
                }

                var contractJson = token["contract"];
                var contract = contractJson == null ? string.Empty : contractJson.Value<string>() ?? "";

                var argsJson = token["args"];
                var args = argsJson == null
                    ? new JArray()
                    : argsJson is JArray ? (JArray)argsJson : new JArray(argsJson);

                invocation = new LaunchInvocation(contract, operation, args);
                return true;
            }
        }

        public struct OracleResponseInvocation
        {
            public readonly string Url;
            public readonly string Callback;
            public readonly string Result;
            public readonly string Filter;
            public readonly OracleResponseCode Code;
            public readonly JToken? UserData;
            public readonly long GasForResponse;

            public OracleResponseInvocation(OracleResponseCode code, string url, string callback, string result, string filter, JToken? userData, long gasForResponse)
            {
                Code = code;
                Url = url;
                Callback = callback;
                Filter = filter;
                UserData = userData;
                Result = result;
                GasForResponse = gasForResponse;
            }

            public static bool TryFromJson(JToken token, out OracleResponseInvocation invocation)
            {
                if (token.Type == JTokenType.Object)
                {
                    var response = token["oracle-response"];
                    if (response != null)
                    {
                        var url = response.Value<string>("url");
                        var callback = response.Value<string>("callback");
                        var result = response["result"] != null
                            ? response.Value<string>()
                            : (TryLoadResultFile(response, out var resultFileContents) ? resultFileContents : null);

                        if (!string.IsNullOrEmpty(url)
                            && !string.IsNullOrEmpty(callback)
                            && result != null)
                        {
                            var filter = response.Value<string>("filter") ?? string.Empty;
                            var code = response["code"] == null
                                ? OracleResponseCode.Success
                                : Enum.Parse<OracleResponseCode>(token.Value<string>("code") ?? "", true);
                            var gas = response["gas"] == null ? (long)0 : token.Value<long>("gas");

                            invocation = new OracleResponseInvocation(code, url, callback, result, filter, token["user-data"], gas);
                            return true;
                        }
                    }
                }

                invocation = default;
                return false;

                static bool TryLoadResultFile(JToken response, out string resultFileContents)
                {
                    var resultFile = response.Value<string>("result-file");
                    if (!string.IsNullOrEmpty(resultFile) && System.IO.File.Exists(resultFile))
                    {
                        try
                        {
                            resultFileContents = System.IO.File.ReadAllText(resultFile);
                            return true;
                        }
                        catch (Exception)
                        {
                        }
                    }

                    resultFileContents = null!;
                    return false;
                }
            }
        }
    }
}