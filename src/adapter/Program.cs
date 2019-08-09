﻿using McMaster.Extensions.CommandLineUtils;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol;
using System;
using System.IO;

namespace Neo.DebugAdapter
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

            NeoDebugAdapter adapter = new NeoDebugAdapter(
                Console.OpenStandardInput(),
                Console.OpenStandardOutput(),
                (cat,msg) => LogMessage(msg, cat));

            adapter.Protocol.LogMessage += (sender, args) => LogMessage(args.Message, args.Category);
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