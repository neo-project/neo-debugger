using McMaster.Extensions.CommandLineUtils;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol;
using System;
using System.IO;

namespace Neo.DebugAdapter
{
    class Program
    {
        static void Main(string[] args) => CommandLineApplication.Execute<Program>(args);

        readonly string logFile;

        public Program()
        {
            logFile = Path.Combine(@"C:\Users\harry\neodebug", $"neo-debug-{DateTime.Now:yyMMdd-hhmmss}.log");
        }


        public void Log(string message, LogCategory category = LogCategory.Trace)
        {
            File.AppendAllText(logFile, $"\n{category} {message}");
        }

        [Option]
        bool Debug { get; }
        
        private void OnExecute(CommandLineApplication app, IConsole console)
        {
            if (Debug)
            {
                System.Diagnostics.Debugger.Launch();
            }

            NeoDebugAdapter adapter = new NeoDebugAdapter(
                Console.OpenStandardInput(),
                Console.OpenStandardOutput(),
                (cat,msg) => Log(msg, cat));

            adapter.Protocol.LogMessage += (sender, args) => Log(args.Message, args.Category);
            adapter.Run();
        }
    }
}
