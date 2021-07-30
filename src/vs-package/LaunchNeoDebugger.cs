using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Globalization;
using System.IO;
using System.Linq;
using Task = System.Threading.Tasks.Task;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NeoDebug.VS
{
    /// <summary>
    /// Command handler
    /// </summary>
    internal sealed class LaunchNeoDebugger
    {
        /// <summary>
        /// Command ID.
        /// </summary>
        public const int CommandId = 0x0100;

        /// <summary>
        /// Command menu group (command set GUID).
        /// </summary>
        public static readonly Guid CommandSet = new Guid("068f4a6b-9e7b-41e5-b483-4ee839895511");

        /// <summary>
        /// VS Package that provides this command, not null.
        /// </summary>
        private readonly AsyncPackage package;

        private readonly string tempLaunchPath = Path.GetTempFileName();
        private readonly string adapterPath = GetAdapterPath();

        /// <summary>
        /// Initializes a new instance of the <see cref="LaunchNeoDebugger"/> class.
        /// Adds our command handlers for menu (commands must exist in the command table file)
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        /// <param name="commandService">Command service to add command to, not null.</param>
        private LaunchNeoDebugger(AsyncPackage package, OleMenuCommandService commandService)
        {
            this.package = package ?? throw new ArgumentNullException(nameof(package));
            commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));

            var menuCommandID = new CommandID(CommandSet, CommandId);
            var menuItem = new MenuCommand(this.Execute, menuCommandID);
            commandService.AddCommand(menuItem);
        }

        /// <summary>
        /// Gets the instance of the command.
        /// </summary>
        public static LaunchNeoDebugger Instance
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the service provider from the owner package.
        /// </summary>
        private Microsoft.VisualStudio.Shell.IAsyncServiceProvider AsyncServiceProvider => package;
        private IServiceProvider ServiceProvider => package;

        /// <summary>
        /// Initializes the singleton instance of the command.
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        public static async Task InitializeAsync(AsyncPackage package)
        {
            // Switch to the main thread - the call to AddCommand in LaunchNeoDebugger's constructor requires
            // the UI thread.
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);

            OleMenuCommandService commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
            Instance = new LaunchNeoDebugger(package, commandService);
        }

        /// <summary>
        /// This function is the callback used to execute the command when the menu item is clicked.
        /// See the constructor to see how the menu item is associated with this function using
        /// OleMenuCommandService service and MenuCommand class.
        /// </summary>
        /// <param name="sender">Event sender.</param>
        /// <param name="e">Event args.</param>
        private void Execute(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var dte = ServiceProvider.GetService<SDTE, DTE2>();
            var launchConfigs = GetLaunchJsonFiles(dte.Solution)
                .SelectMany(ParseLaunchJsonFile)
                .Where(t => t.config.TryGetValue("type", out JToken type) && type.Value<string>() == "neo-contract")
                .ToList();

            if (launchConfigs.Count == 0)
            {
                _ = VsShellUtilities.ShowMessageBox(
                    package,
                    "No neo-contract Launch Configurations Found",
                    nameof(LaunchNeoDebugger),
                    OLEMSGICON.OLEMSGICON_INFO,
                    OLEMSGBUTTON.OLEMSGBUTTON_OK,
                    OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
                return;
            }

            JObject launchConfig = launchConfigs[0].config;
            if (launchConfig.Count > 1)
            {
                var viewModel = new LaunchConfigSelectionViewModel(launchConfigs);
                var dialog = new LaunchConfigSelectionDialog() { DataContext = viewModel };
                if (dialog.ShowModal() != true) return;
                launchConfig = viewModel.SelectedLaunchConfig.Config;
            }

            try
            {
                LaunchDebugger(launchConfig);
            }
            catch (Exception ex)
            {
                _ = VsShellUtilities.ShowMessageBox(
                    package,
                    string.Format(CultureInfo.CurrentCulture, "Debugger Launch failed.  Error: {0}", ex.Message),
                    nameof(LaunchNeoDebugger),
                    OLEMSGICON.OLEMSGICON_WARNING,
                    OLEMSGBUTTON.OLEMSGBUTTON_OK,
                    OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
            }
        }

        private void LaunchDebugger(JObject config)
        {
            const string NeoDebugAdapterId = "ba0544e5-b299-4a4d-b6bb-c62e1c6cfa71";
            const string DebugAdapterHostPackageCmdSet = "0ddba113-7ac1-4c6e-a2ef-dcac3f9e731e";
            const int DebugAdapterHostLaunchCommandId = 0x0101;

            ThreadHelper.ThrowIfNotOnUIThread();

            try
            {
                var solution = ServiceProvider.GetService<SVsSolution, IVsSolution>();
                var dte = ServiceProvider.GetService<SDTE, DTE2>();

                var (solutionDirectory, _, _) = solution.GetSolutionInfo();
                solutionDirectory = solutionDirectory.TrimEnd('\\').Replace('\\', '/');

                config = JObject.Parse(config.ToString().Replace("${workspaceFolder}", solutionDirectory));
                config["$adapter"] = adapterPath;

                var configText = config.ToString(Formatting.Indented);
                File.WriteAllText(tempLaunchPath, configText);

                string parameters = FormattableString.Invariant($@"/LaunchJson:""{tempLaunchPath}"" /EngineGuid:""{NeoDebugAdapterId}""");
                dte.Commands.Raise(DebugAdapterHostPackageCmdSet, DebugAdapterHostLaunchCommandId, parameters, IntPtr.Zero);
            }
            catch (Exception ex)
            {
                _ = VsShellUtilities.ShowMessageBox(
                    package,
                    string.Format(CultureInfo.CurrentCulture, "Launch failed.  Error: {0}", ex.Message),
                    nameof(LaunchNeoDebugger),
                    OLEMSGICON.OLEMSGICON_WARNING,
                    OLEMSGBUTTON.OLEMSGBUTTON_OK,
                    OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
            }
        }

        IEnumerable<string> GetLaunchJsonFiles(Solution sln)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            foreach (Project project in sln.Projects)
            {
                foreach (var config in GetLaunchJsonFiles(project.ProjectItems))
                {
                    yield return config;
                }
            }
        }

        IEnumerable<string> GetLaunchJsonFiles(ProjectItems items)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            short fileNameIndex = 0;
            foreach (ProjectItem projectItem in items)
            {
                if (projectItem.ProjectItems != null && projectItem.ProjectItems.Count != 0)
                {
                    foreach (var config in GetLaunchJsonFiles(projectItem.ProjectItems))
                    {
                        yield return config;
                    }
                }

                if (projectItem.SubProject != null && projectItem.SubProject.ProjectItems != null)
                {
                    foreach (var config in GetLaunchJsonFiles(projectItem.SubProject.ProjectItems))
                    {
                        yield return config;
                    }
                }

                string fileName = null;
                try
                {
                    fileName = projectItem.FileNames[fileNameIndex];
                }
                catch
                {
                    // Some project systems use 0-based indexes, others are 1-based - try the other option
                    fileNameIndex = (short)((fileNameIndex == 0) ? 1 : 0);

                    try
                    {
                        fileName = projectItem.FileNames[fileNameIndex];
                    }
                    catch
                    {
                    }
                }

                if (!string.IsNullOrEmpty(fileName) &&
                    Path.GetFileName(fileName).StartsWith("launch", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(Path.GetExtension(fileName), ".json", StringComparison.OrdinalIgnoreCase))
                {
                    yield return fileName;
                }
            }
        }

        private IEnumerable<(string file, JObject config)> ParseLaunchJsonFile(string launchFilePath)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var solution = ServiceProvider.GetService<SVsSolution, IVsSolution>();
            var (solutionDirectory, _, _) = solution.GetSolutionInfo();
            var relativeLaunchFilePath = launchFilePath.TrimPrefix(solutionDirectory);

            if (TryLoadJObject(launchFilePath, out JObject json)
                && json.TryGetValue("version", out JToken version)
                && version.Value<string>() == "0.2.0"
                && json.TryGetValue("configurations", out JToken configurations)
                && configurations is JArray configArray)
            {
                for (int i = 0; i < configArray.Count; i++)
                {
                    if (configArray[i] is JObject config)
                    {
                        yield return (relativeLaunchFilePath, config);
                    }
                }
            }
            else
            {
                yield return (relativeLaunchFilePath, json);
            }
        }

        private static bool TryLoadJObject(string path, out JObject json)
        {
            try
            {
                json = JObject.Parse(File.ReadAllText(path));
                return true;
            }
            catch
            {
                json = null;
                return false;
            }
        }

        private static string GetAdapterPath()
        {
            string codebase = typeof(NeoDebuggerPackage).Assembly.CodeBase;
            var uri = new Uri(codebase, UriKind.Absolute);
            return Path.Combine(Path.GetDirectoryName(uri.LocalPath), "adapter\\neodebug-3-adapter.exe");
        }
    }
}
