using McMaster.Extensions.CommandLineUtils;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using NeoDebug.Models;
using NeoFx;
using NeoFx.Models;
using NeoFx.Storage;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
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


        private static IExecutionEngine CreateExecutionEngine(Contract contract, LaunchArguments arguments)
        {

            //EmulatedRuntime GetRuntime()
            //{
            //    if (arguments.ConfigurationProperties.TryGetValue("runtime", out var token))
            //    {
            //        var trigger = "verification".Equals(token.Value<string>("trigger"))
            //            ? EmulatedRuntime.TriggerType.Verification : EmulatedRuntime.TriggerType.Application;

            //        var witnessesJson = token["witnesses"];
            //        if (witnessesJson?.Type == JTokenType.Object)
            //        {
            //            var result = witnessesJson.Value<bool>("check-result");
            //            return new EmulatedRuntime(trigger, result);
            //        }
            //        if (witnessesJson?.Type == JTokenType.Array)
            //        {
            //            var witnesses = witnessesJson
            //                .Select(t => t.Value<string>().ParseBigInteger().ToByteArray());
            //            return new EmulatedRuntime(trigger, witnesses);
            //        }
            //    }

            //    return new EmulatedRuntime();
            //}

            //IBlockchainStorage? GetBlockchain()
            //{
            //    if (arguments.ConfigurationProperties.TryGetValue("checkpoint", out var checkpoint))
            //    {
            //        return NeoFx.RocksDb.RocksDbStore.OpenCheckpoint(checkpoint.Value<string>());
            //    }

            //    return null;
            //}

            //var blockchain = GetBlockchain();

            //return DebugExecutionEngine.Create(contract, GetStorage(), GetRuntime());

            return null;
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
