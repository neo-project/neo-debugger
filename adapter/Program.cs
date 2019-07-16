using McMaster.Extensions.CommandLineUtils;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol;
using System;
using System.Collections.Generic;

namespace Neo.DebugAdapter
{
    class Program
    {
        static void Main(string[] args) => CommandLineApplication.Execute<Program>(args);

        [Option]
        bool Debug { get; }

        static List<string> log = new List<string>();

        const string LOG_FILE = @"C:\Users\harry\neodebug.log";
        public static void Log(string message, LogCategory category = LogCategory.Trace)
        {
            System.IO.File.AppendAllText(LOG_FILE, $"\n{category} {message}");
        }

        private void OnExecute(CommandLineApplication app, IConsole console)
        {
            if (System.IO.File.Exists(LOG_FILE))
                System.IO.File.Delete(LOG_FILE);

            if (Debug)
            {
                System.Diagnostics.Debugger.Launch();

                var proc = System.Diagnostics.Process.GetCurrentProcess();
                var id = System.Diagnostics.Process.GetCurrentProcess().Id;

                ;
            }

            NeoDebugAdapter adapter = new NeoDebugAdapter(Console.OpenStandardInput(), Console.OpenStandardOutput());
            adapter.Protocol.LogMessage += (sender, args) => Log(args.Message, args.Category);
            adapter.Run();

            if (Debug)
            {
                adapter.Protocol.WaitForReader();
            }

            ;
        }
    }
}
