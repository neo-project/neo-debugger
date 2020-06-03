using McMaster.Extensions.CommandLineUtils;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo.VM;
using Neo.SmartContract;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Text;

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
                CreateDebugSession,
                Console.OpenStandardInput(),
                Console.OpenStandardOutput(),
                LogMessage,
                defaultDebugView);

            adapter.Run();
        }

        static IDebugSession CreateDebugSession(LaunchArguments launchArguments,
            Action<DebugEvent> sendEvent, DebugView defaultDebugView)
        {            
            var program = launchArguments.ConfigurationProperties["program"].Value<string>();
            var contract = LoadContract(program);

            using var builder = new ScriptBuilder();
            builder.EmitAppCall(contract.ScriptHash, "add", 2, 2);
            var invokeScript = builder.ToArray();

            var engine = new DebugExecutionEngine();
            engine.LoadScript(builder.ToArray());

            return new DebugSession(engine, sendEvent);

            static NefFile LoadContract(string contractPath)
            {
                using var stream = File.OpenRead(contractPath);
                using var reader = new BinaryReader(stream, Encoding.UTF8, false);
                return Neo.IO.Helper.ReadSerializable<NefFile>(reader);
            }
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
