using McMaster.Extensions.CommandLineUtils;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol;
using System;
using System.IO;

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
                "Neo-Debugger",
                "logs");

            if (!Directory.Exists(neoDebugLogPath))
            {
                Directory.CreateDirectory(neoDebugLogPath);
            }

            logFile = Path.Combine(neoDebugLogPath, $"{DateTime.Now:yyMMdd-hhmmss}.log");
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
                DebugExecutionEngine.Create,
                Crypto.Hash160,
                (cat, msg) => LogMessage(msg, cat));

            adapter.Run();
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
