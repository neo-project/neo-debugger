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

        // public struct OracleResponseInvocation
        // {
        //     public readonly string Url;
        //     public readonly string Callback;
        //     public readonly string Filter;
        //     public readonly OracleResponseCode Code;
        //     public readonly JToken? UserData;
        //     public readonly JToken Result;

        //     public OracleResponseInvocation(OracleResponseCode code, string url, string callback, JToken result, string filter, JToken? userData)
        //     {
        //         Code = code;
        //         Url = url;
        //         Callback = callback;
        //         Result = result;
        //         Filter = filter;
        //         UserData = userData;
        //     }

        //     public static OracleResponseInvocation FromJson(JToken token)
        //     {
        //         var url = token.Value<string>("url") ?? throw new Exception("url oracle response property missing");
        //         var callback = token.Value<string>("callback") ?? throw new Exception("callback oracle response property missing");
        //         var result = token["result"] ?? throw new Exception("result oracle response property missing");

        //         var filter = token.Value<string>("filter") ?? string.Empty;
        //         var userData = token["userData"]; 
        //         var code = OracleResponseCode.Success; // TODO: parse code from json

        //         return new OracleResponseInvocation(code, url, callback, result, filter, userData);
        //     }
        // }

        // oracleResponse invocation launch.config schema: 
        // {
        //     "type": "object",
        //     "required": [
        //         "oracleResponse"
        //     ],
        //     "properties": {
        //         "oracleResponse": {
        //             "type": "object",
        //             "required": [
        //                 "url",
        //                 "callback",
        //                 "result"
        //             ],
        //             "properties": {
        //                 "url": {
        //                     "type": "string"
        //                 },
        //                 "callback": {
        //                     "type": "string"
        //                 },
        //                 "filter": {
        //                     "type": "string"
        //                 },
        //                 "code": {
        //                     "type": "number"
        //                 },
        //                 "userData": {},
        //                 "result": {}
        //             }
        //         }
        //     }
        // },
    }
}
