using McMaster.Extensions.CommandLineUtils;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol;
using System;
using System.IO;

namespace NeoDebug.Neo3
{

    class Program
    {
        private static void Main(string[] args) => CommandLineApplication.Execute<Program>(args);

        private readonly string logFile;

        [Option]
        private bool Debug { get; }

        [Option]
        private bool Log { get; }

        [Option("-v|--debug-view")]
        private string DefaultDebugView { get; } = string.Empty;

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
                while (!System.Diagnostics.Debugger.IsAttached)
                {
                    System.Threading.Thread.Sleep(100);
                }
            }

            var defaultDebugView = DefaultDebugView.Length > 0
                ? Enum.Parse<DebugView>(DefaultDebugView, true)
                : DebugView.Source;

            if (defaultDebugView == DebugView.Toggle)
                throw new ArgumentException(nameof(DefaultDebugView));

            var adapter = new DebugAdapter(
                (a,b,c) => throw new NotImplementedException(),                
                Console.OpenStandardInput(),
                Console.OpenStandardOutput(),
                (cat, msg) => LogMessage(msg, cat),
                defaultDebugView);

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
