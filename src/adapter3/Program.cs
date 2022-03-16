using System;
using System.IO;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol;
using McMaster.Extensions.CommandLineUtils;

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

        [Option("-s|--storage-view")]
        private string StorageView { get; } = string.Empty;

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

            var defaultDebugView = Enum.TryParse<DebugView>(StorageView, true, out var _debugView)
                ? _debugView : DebugView.Source;

            var storageView = Enum.TryParse<StorageView>(StorageView, true, out var _storageView)
                ? _storageView : Neo3.StorageView.FullKey;

            if (defaultDebugView == DebugView.Toggle)
                throw new ArgumentException(nameof(DefaultDebugView));

            var adapter = new DebugAdapter(
                Console.OpenStandardInput(),
                Console.OpenStandardOutput(),
                LogMessage,
                defaultDebugView,
                storageView);

            adapter.Run();
        }

        void LogMessage(LogCategory category, string message)
        {
            if (Log)
            {
                File.AppendAllText(logFile, $"\n{category} {message}");
            }
        }
    }
}
