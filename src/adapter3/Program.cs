using McMaster.Extensions.CommandLineUtils;
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

        // TODO: async?
        static IDebugSession CreateDebugSession(LaunchArguments launchArguments,
            Action<DebugEvent> sendEvent, DebugView defaultDebugView)
        {            
            var program = launchArguments.ConfigurationProperties["program"].Value<string>();
            var (contract, manifest) = LoadContract(program);

            IStore memoryStore = new MemoryStore();
            AddContract(memoryStore, contract, manifest);

            using var builder = new ScriptBuilder();
            builder.EmitAppCall(contract.ScriptHash, "add", 2, 2);
            var invokeScript = builder.ToArray();

            var engine = new DebugApplicationEngine(new SnapshotView(memoryStore));
            engine.LoadScript(builder.ToArray());

            return new DebugSession(engine, sendEvent);

            static void AddContract(IStore store, NefFile contract, ContractManifest manifest)
            {
                var snapshotView = new SnapshotView(store);

                var contractState = new ContractState
                {
                    Id = snapshotView.ContractId.GetAndChange().NextId++,
                    Script = contract.Script,
                    Manifest = manifest
                };
                snapshotView.Contracts.Add(contract.ScriptHash, contractState);

                snapshotView.Commit();
            }

            static (NefFile contract, ContractManifest manifest) LoadContract(string contractPath)
            {
                var manifestPath = Path.ChangeExtension(contractPath, ".manifest.json");
                var manifest = ContractManifest.Parse(File.ReadAllBytes(manifestPath));

                using var stream = File.OpenRead(contractPath);
                using var reader = new BinaryReader(stream, Encoding.UTF8, false);
                var nefFile = Neo.IO.Helper.ReadSerializable<NefFile>(reader);

                return (nefFile, manifest);
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
