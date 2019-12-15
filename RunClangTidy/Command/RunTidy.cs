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
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Shell.TableManager;
using Microsoft.VisualStudio.VCProjectEngine;
using VsUtil.Util;
using Task = System.Threading.Tasks.Task;

namespace CodingToolBox.Command
{
    /// <summary>
    /// Command handler
    /// </summary>
    internal sealed class RunTidy
    {
        private ExtensionOutput m_output;

        /// <summary>
        /// Command ID.
        /// </summary>
        public const int CommandId = 0x0101;

        /// <summary>
        /// Command menu group (command set GUID).
        /// </summary>
        public static readonly Guid CommandSet = new Guid("c191f2ed-a77f-4fbf-a1e0-4ee9c730bcf5");

        /// <summary>
        /// VS Package that provides this command, not null.
        /// </summary>
        private readonly AsyncPackage package;
        private readonly OleMenuCommand menuItem;

        private IAnalysisFailuresService m_errorService;
        private CppProject m_cppSupport;
        /// <summary>
        /// Initializes a new instance of the <see cref="RunTidy"/> class.
        /// Adds our command handlers for menu (commands must exist in the command table file)
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        /// <param name="commandService">Command service to add command to, not null.</param>
        private RunTidy(AsyncPackage package, OleMenuCommandService commandService, IAnalysisFailuresService errorService)
        {
            this.package = package ?? throw new ArgumentNullException(nameof(package));
            commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));
            m_output = new ExtensionOutput(package, "CLang Output", ExtensionOutput.DefaultOutputWindowGuid);
            var menuCommandID = new CommandID(CommandSet, CommandId);
            menuItem = new OleMenuCommand(this.Execute, menuCommandID);
            menuItem.BeforeQueryStatus += new EventHandler(OnBeforeQueryStatus);
            commandService.AddCommand(menuItem);
            m_errorService = errorService;

            var dteService = package.GetService<EnvDTE.DTE, EnvDTE.DTE>();
            m_cppSupport = new CppProject(dteService);

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

            var project = m_cppSupport.GetProjectFromSelected();
            if (project != null && project.Object is VCProject)
                myCommand.Visible = true;
            else
                myCommand.Visible = false;
        }

        /// <summary>
        /// Gets the instance of the command.
        /// </summary>
        public static RunTidy Instance
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
            // Switch to the main thread - the call to AddCommand in RunTidy's constructor requires
            // the UI thread.
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);
            OleMenuCommandService commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
            Instance = new RunTidy(package, commandService, ((RunClangTidy)package).m_analysisService);
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

            var project = m_cppSupport.GetProjectFromSelected();
            var vcProject = project.Object as VCProject;
            var configuration = m_cppSupport.GetActiveConfiguration(project);
            m_cppSupport.Save(project); // ensure that project is saved before we do anything
            var hierarchy = m_cppSupport.ToHierarchy(project as EnvDTE.Project);
            var compileDatabaseDirectory = new FileInfo(project.FileName).DirectoryName;
            
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
            var visitor = new CollectProjectFilesVisitor(m_output, m_cppSupport);
            m_cppSupport.VisitHierarchy(hierarchy, visitor);
            if (!new FileInfo(CLangHelpers.GetCompileDatabasePath(compileDatabaseDirectory)).Exists)
                CLangHelpers.CreateCompilationDatabase(package, compileCommand);
            TidyControl.RunningTidy = new CancellationTokenSource();
            var cancel = TidyControl.RunningTidy.Token;

            var errorProvider = new AnalysisOutputParser(visitor.ProjectFiles.Count); 
            var executeShell = new ExecuteShellCommand(m_output, errorProvider);

            _ = ExecuteTidyAsync(executeShell, clangPath, compileDatabaseDirectory, clangOptions, visitor.ProjectFiles, cancel);
        }

        private async System.Threading.Tasks.Task ExecuteTidyAsync(
            ExecuteShellCommand executor,
            string tidyPath,
            string compileDatabasePath,
            string tidyOptions,
            List<string> files, 
            CancellationToken cancel)
        {
            IInvokeTidyService service = await package.GetServiceAsync(typeof(SInvokeTidyService))
                as IInvokeTidyService;
            if (service == null)
                return;
           await service.ExecuteTidyAsync(executor, tidyPath, compileDatabasePath, tidyOptions, files, cancel)
                .ContinueWith( _ =>
                {
                    executor.Done();
                    m_errorService.SetAnalysisFailures(executor.Failures);
                    TidyControl.RunningTidy.Dispose();
                    TidyControl.RunningTidy = null;
                }, TaskScheduler.Default);
        }
        
    }



    class CollectProjectFilesVisitor : ProjectItemVisitor
    {
        private readonly List<string> compileDatabase = new List<string>();
        public List<string> ProjectFiles => compileDatabase;
        private readonly ExtensionOutput m_output;
        private readonly CppProject m_project;
        public CollectProjectFilesVisitor(ExtensionOutput output, CppProject project)
        {
            m_output = output;
            m_project = project;
        }
        public void EnterProjectItem(ProjectItem item)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var vcFile = item.Object as VCFile;
            if (vcFile == null)
                return;

            if (vcFile.ItemType != CompilerSettings.CLCOMPILE)
                return;
            ProjectFiles.Add(vcFile.FullPath);
        }

        public void LeaveProjectItem(ProjectItem item)
        {
        }
    };
}

