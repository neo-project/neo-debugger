﻿using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Task = System.Threading.Tasks.Task;

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

        const string DebugAdapterHostPackageCmdSet = "0ddba113-7ac1-4c6e-a2ef-dcac3f9e731e";
        const int LaunchCommandId = 0x0101;
        static readonly Guid NeoDebugAdapterId = new Guid("BA0544E5-B299-4A4D-B6BB-C62E1C6CFA71");


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
        private Microsoft.VisualStudio.Shell.IAsyncServiceProvider ServiceProvider
        {
            get
            {
                return this.package;
            }
        }

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

            var configs = new List<(string path, bool isUserPath)>();
            DTE2 dte = (DTE2)((IServiceProvider)this.package).GetService(typeof(SDTE));
            if (dte != null)
            {
                foreach (Project project in dte.Solution.Projects)
                {
                    GetProjectLaunchConfigurations(project.ProjectItems, configs);
                }
            }

            if (configs.Count == 0)
            {
                VsShellUtilities.ShowMessageBox(
                    package,
                    "No Launch Configurations Found",
                    nameof(LaunchNeoDebugger),
                    OLEMSGICON.OLEMSGICON_INFO,
                    OLEMSGBUTTON.OLEMSGBUTTON_OK,
                    OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
            }
            else
            {
                try
                {
                    string parameters = FormattableString.Invariant($@"/LaunchJson:""{configs[0].path}"" /EngineGuid:""{NeoDebugAdapterId}""");
                    dte.Commands.Raise(DebugAdapterHostPackageCmdSet, LaunchCommandId, parameters, IntPtr.Zero);
                }
                catch (Exception ex)
                {
                    VsShellUtilities.ShowMessageBox(
                        package,
                        string.Format(CultureInfo.CurrentCulture, "Launch failed.  Error: {0}", ex.Message),
                        nameof(LaunchNeoDebugger),
                        OLEMSGICON.OLEMSGICON_WARNING,
                        OLEMSGBUTTON.OLEMSGBUTTON_OK,
                        OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
                }
            }

        }
    
        private void GetProjectLaunchConfigurations(ProjectItems items, List<(string path, bool isUserPath)> configs)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            short fileNameIndex = 0;
            foreach (ProjectItem projectItem in items)
            {
                if (projectItem.ProjectItems != null && projectItem.ProjectItems.Count != 0)
                {
                    GetProjectLaunchConfigurations(projectItem.ProjectItems, configs);
                }

                if (projectItem.SubProject != null && projectItem.SubProject.ProjectItems != null)
                {
                    GetProjectLaunchConfigurations(projectItem.SubProject.ProjectItems, configs);
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
                    configs.Add((fileName, false));
                }
            }
        }
    }
}
