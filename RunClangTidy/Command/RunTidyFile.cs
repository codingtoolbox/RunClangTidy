using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.ComponentModel.Design;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CodingToolBox.Service;
using CodingToolBox.Util;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using VsUtil.Util;
using Task = System.Threading.Tasks.Task;

namespace CodingToolBox.Command
{
    /// <summary>
    /// Command handler
    /// </summary>
    internal sealed class RunTidyFile
    {
        private ExtensionOutput m_output;
        
        private readonly CppProject m_cppProject;
        /// <summary>
        /// Command ID.
        /// </summary>
        public const int CommandId = 256;

        /// <summary>
        /// Command menu group (command set GUID).
        /// </summary>
        public static readonly Guid CommandSet = new Guid("551573ef-a991-41ad-bd87-82a2649cd38b");

        /// <summary>
        /// VS Package that provides this command, not null.
        /// </summary>
        private readonly AsyncPackage package;

        IAnalysisFailuresService m_errorService;


        /// <summary>
        /// Initializes a new instance of the <see cref="RunTidyFile"/> class.
        /// Adds our command handlers for menu (commands must exist in the command table file)
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        /// <param name="commandService">Command service to add command to, not null.</param>
        private RunTidyFile(AsyncPackage package, OleMenuCommandService commandService, IAnalysisFailuresService errorService)
        {
            this.package = package ?? throw new ArgumentNullException(nameof(package));
            commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));

            var menuCommandID = new CommandID(CommandSet, CommandId);
            var menuItem = new OleMenuCommand(this.Execute, menuCommandID);
            menuItem.BeforeQueryStatus += new EventHandler(OnBeforeQueryStatus);
            commandService.AddCommand(menuItem);

            var dteService = package.GetService<EnvDTE.DTE, EnvDTE.DTE>();
            m_cppProject = new CppProject(dteService);
            m_errorService = errorService;
        }

        private void OnBeforeQueryStatus(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var myCommand = sender as OleMenuCommand;
            if (myCommand == null)
                return;
            if (TidyControl.RunningTidy == null)
                myCommand.Text = Resources.RunTidy;
            else
                myCommand.Text = Resources.TidyInProgress;
            var selection = m_cppProject.GetVCFilesFromSelected();
            if (selection.Count > 0)
                myCommand.Visible = true;
            else
                myCommand.Visible = false;
        }

        /// <summary>
        /// Gets the instance of the command.
        /// </summary>
        public static RunTidyFile Instance
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
            // Switch to the main thread - the call to AddCommand in RunTidyFile's constructor requires
            // the UI thread.
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);

            OleMenuCommandService commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
            Instance = new RunTidyFile(package, commandService, ((RunClangTidy)package).m_analysisService);
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
            if (TidyControl.RunningTidy != null)
            {
                TidyControl.RunningTidy.Cancel();
                m_output.Write(Resources.Cancelled + "\n");
                return;
            }
            m_output = new ExtensionOutput(package, "CLang Output", ExtensionOutput.DefaultOutputWindowGuid);
            var mySettings = new Settings<PropertyPage.PropertyPage>(package);
            var clangPath = mySettings.GetPage().CLangTidy;
            var compileCommand = mySettings.GetPage().CompileCommand;
            if (clangPath == null || clangPath.Length == 0 ||
                compileCommand == null || compileCommand.Length == 0)
            {
                TidyControl.CancelAndShowHelp(package);
                return;
            }
            var clangOptions = mySettings.GetPage().AnalysisOptions;
            if (clangOptions == null)
                clangOptions = "";
            var selection = m_cppProject.GetVCFilesFromSelected();
            Dictionary<string, List<string>> projectsToFiles = new Dictionary<string, List<string>>();
            foreach (var (vcFile, project) in selection)
            {
                var compileDatabaseDirectory = new FileInfo(project.FileName).DirectoryName;
                var file = vcFile.FullPath;
                if (!projectsToFiles.ContainsKey(compileDatabaseDirectory))
                    projectsToFiles.Add(compileDatabaseDirectory, new List<string>());
                if (!new FileInfo(CLangHelpers.GetCompileDatabasePath(compileDatabaseDirectory)).Exists)
                    CLangHelpers.CreateCompilationDatabase(package, compileCommand);

                projectsToFiles[compileDatabaseDirectory].Add(file);
            }
            TidyControl.RunningTidy = new CancellationTokenSource();
            var cancel = TidyControl.RunningTidy.Token;
            var errorProvider = new AnalysisOutputParser(selection.Count);
            var executeShell = new ExecuteShellCommand(m_output, errorProvider);

            _ = ExecuteTidySetAsync(executeShell, clangPath, projectsToFiles, clangOptions, cancel);
        }

        private async System.Threading.Tasks.Task ExecuteTidySetAsync(ExecuteShellCommand executeShell,
                string clangPath, Dictionary<string, List<string>> projectToFiles, string clangOptions, CancellationToken cancel)
        {
            IInvokeTidyService service = await package.GetServiceAsync(typeof(SInvokeTidyService))
                as IInvokeTidyService;
            if (service == null)
                return;
            try
            {
                List<Task> tasks = new List<Task>();
                foreach (var compileDatabase in projectToFiles.Keys)
                {
                    tasks.Add(service.ExecuteTidyAsync(executeShell, clangPath, compileDatabase, clangOptions, projectToFiles[compileDatabase]
                            , cancel));
                }
                await Task.WhenAll(tasks.ToArray()).ContinueWith(_ =>
                {
                    executeShell.Done();
                    m_errorService.SetAnalysisFailures(executeShell.Failures);
                    TidyControl.RunningTidy.Dispose();
                    TidyControl.RunningTidy = null;
                }, TaskScheduler.Default);

            }
            catch (Exception)
            {
                TidyControl.RunningTidy.Dispose();
                TidyControl.RunningTidy = null;
            }

        }

    }
}
