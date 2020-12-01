using Neo.Network.P2P.Payloads;
using Newtonsoft.Json.Linq;
using System;

namespace NeoDebug.Neo3
{
    internal static partial class LaunchConfigParser
    {
        public struct InvokeFileInvocation
        {
            public readonly string Path;

            public InvokeFileInvocation(string path)
            {
                Path = path;
            }
        }

        public struct LaunchInvocation
        {
            public readonly string Operation;
            public readonly JArray Args;

            public LaunchInvocation(string operation, JArray args)
            {
                Operation = operation;
                Args = args;
            }

            public static bool TryFromJson(JToken token, out LaunchInvocation invocation)
            {
                var operation = token.Value<string>("operation");
                if (string.IsNullOrEmpty(operation))
                {
                    invocation = default;
                    return false;
                }
                
                var args = token["args"];
                var array = args == null
                    ? new JArray()
                    : args is JArray ? (JArray)args : new JArray(args);

                invocation = new LaunchInvocation(operation, array);
                return true;
            }
        }

        public struct OracleResponseInvocation
        {
            public readonly string Url;
            public readonly string Callback;
            public readonly string Filter;
            public readonly OracleResponseCode Code;
            public readonly JToken? UserData;
            public readonly JToken Result;
            public readonly long GasForResponse;

            public OracleResponseInvocation(OracleResponseCode code, string url, string callback, string filter, JToken? userData, JToken result, long gasForResponse)
            {
                Code = code;
                Url = url;
                Callback = callback;
                Filter = filter;
                UserData = userData;
                Result = result;
                GasForResponse = gasForResponse;
            }

            public static bool TryFromJson(JToken? token, out OracleResponseInvocation invocation)
            {
                if (token != null)
                {
                    var url = token.Value<string>("url");
                    var callback = token.Value<string>("callback");
                    var result = token["result"];

                    if (!string.IsNullOrEmpty(url) && !string.IsNullOrEmpty(callback) && result != null)
                    {
                        var filter = token.Value<string>("filter") ?? string.Empty;
                        var code = token["code"] == null ? OracleResponseCode.Success : Enum.Parse<OracleResponseCode>(token.Value<string>("code"), true);
                        var gas = token["gas"] == null ? (long)0 : token.Value<long>("gas");

                        invocation = new OracleResponseInvocation(code, url, callback, filter, token["userData"], result, gas);
                        return true;
                    }
                }

                invocation = default;
                return false;
            }
        }
    }
}
