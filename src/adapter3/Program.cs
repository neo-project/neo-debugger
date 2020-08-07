﻿using McMaster.Extensions.CommandLineUtils;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo.VM;
using Neo.SmartContract;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Text;
using Neo.SmartContract.Manifest;
using Neo.Persistence;
using Neo.Ledger;

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

        [Option]
        private bool Trace { get; }

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
                LaunchConfigParser.CreateDebugSession,
                Console.OpenStandardInput(),
                Console.OpenStandardOutput(),
                LogMessage,
                Trace,
                defaultDebugView);

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
