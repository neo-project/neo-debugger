using McMaster.Extensions.CommandLineUtils;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using NeoDebug.Models;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Linq;

namespace NeoDebug.Adapter
{

    internal class Program
    {
        private static void Main(string[] args) => CommandLineApplication.Execute<Program>(args);

        private readonly string logFile;

        [Option]
        private bool Debug { get; }

        [Option]
        private bool Log { get; }

        public Program()
        {
            var neoDebugLogPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "NEO-Debug",
                "logs");

            if (!Directory.Exists(neoDebugLogPath))
            {
                Directory.CreateDirectory(neoDebugLogPath);
            }

            logFile = Path.Combine(neoDebugLogPath, $"neo-debug-{DateTime.Now:yyMMdd-hhmmss}.log");
        }

        private void OnExecute(CommandLineApplication app, IConsole console)
        {
            if (Debug)
            {
                System.Diagnostics.Debugger.Launch();
            }

            var adapter = new DebugAdapter(
                Console.OpenStandardInput(),
                Console.OpenStandardOutput(),
                CreateExecutionEngine,
                Crypto.Hash160,
                (cat, msg) => LogMessage(msg, cat));

            adapter.Run();
        }

        private static byte[] ConvertString(string value)
        {
            if (value.TryParseBigInteger(out var bigInteger))
            {
                return bigInteger.ToByteArray();
            }

            return System.Text.Encoding.UTF8.GetBytes(value);
        }

        static byte[] ConvertString(JToken token) => ConvertString(token.Value<string>());

        private static IExecutionEngine CreateExecutionEngine(Contract contract, LaunchArguments arguments)
        {
            EmulatedStorage GetStorage()
            {
                if (arguments.ConfigurationProperties.TryGetValue("storage", out var token))
                {
                    var items = token.Select(t =>
                        (ConvertString(t["key"]),
                        ConvertString(t["value"]),
                        t.Value<bool?>("constant") ?? false));
                    return new EmulatedStorage(contract.ScriptHash, items);
                }

                return new EmulatedStorage(contract.ScriptHash);
            }

            EmulatedRuntime GetRuntime()
            {
                if (arguments.ConfigurationProperties.TryGetValue("runtime", out var token))
                {
                    var witnessesJson = token["witnesses"];
                    if (witnessesJson?.Type == JTokenType.Object)
                    {
                        var result = witnessesJson.Value<bool>("check-result");
                        return new EmulatedRuntime(result);
                    }
                    if (witnessesJson?.Type == JTokenType.Array)
                    {
                        var _witnesses = witnessesJson
                            .Select(t => t.Value<string>().ParseBigInteger().ToByteArray());
                        return new EmulatedRuntime(_witnesses);
                    }
                }

                return new EmulatedRuntime();
            }

            return DebugExecutionEngine.Create(contract, GetStorage(), GetRuntime());
        }

        public void LogMessage(string message, LogCategory category = LogCategory.Trace)
        {
            if (Log)
            {
                File.AppendAllText(logFile, $"\n{category} {message}");
            }
        }
    }
}
