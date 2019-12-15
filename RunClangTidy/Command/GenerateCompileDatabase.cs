using System;
using System.ComponentModel.Design;
using EnvDTE;
using Microsoft.VisualStudio.Settings;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.FindAllReferences;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.VCProjectEngine;
using CodingToolBox.Util;
using Task = System.Threading.Tasks.Task;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Microsoft.VisualStudio.Shell.Interop;

namespace CodingToolBox.Command
{
    /// <summary>
    /// Command handler
    /// </summary>
    internal sealed class GenerateCompileDatabase
    {
        /// <summary>
        /// Command ID.
        /// </summary>
        public const int CommandId = 0x0100;

        /// <summary>
        /// Command menu group (command set GUID).
        /// </summary>
        public static readonly Guid CommandSet = new Guid("c191f2ed-a77f-4fbf-a1e0-4ee9c730bcf5");

        /// <summary>
        /// VS Package that provides this command, not null.
        /// </summary>
        private readonly AsyncPackage package;

        private readonly CppProject m_cppSupport;

        /// <summary>
        /// Initializes a new instance of the <see cref="GenerateCompileDatabase"/> class.
        /// Adds our command handlers for menu (commands must exist in the command table file)
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        /// <param name="commandService">Command service to add command to, not null.</param>
        private GenerateCompileDatabase(AsyncPackage package, OleMenuCommandService commandService)
        {
            this.package = package ?? throw new ArgumentNullException(nameof(package));
            commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));

            var menuCommandID = new CommandID(CommandSet, CommandId);
            var menuItem = new OleMenuCommand(this.Execute, menuCommandID);
            menuItem.BeforeQueryStatus += new EventHandler(OnBeforeQueryStatus);
            commandService.AddCommand(menuItem);

            var dteService = package.GetService<EnvDTE.DTE, EnvDTE.DTE>();
            m_cppSupport = new CppProject(dteService);
        }

        private void OnBeforeQueryStatus(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var myCommand = sender as OleMenuCommand;
            if (myCommand == null)
                return;
            var project = m_cppSupport.GetProjectFromSelected();
            if (project != null && project.Object is VCProject)
                myCommand.Visible = true;
            else
                myCommand.Visible = false;

        }

        /// <summary>
        /// Gets the instance of the command.
        /// </summary>
        public static GenerateCompileDatabase Instance
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
            // Switch to the main thread - the call to AddCommand in ExtendedSearch's constructor requires
            // the UI thread.
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);

            OleMenuCommandService commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
            Instance = new GenerateCompileDatabase(package, commandService);
        }


        void listPropertySheetCollection(ExtensionOutput output, dynamic collection, int i)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            foreach (var property in collection)
            {
                if (property is VCPropertySheet sheet)
                {
                    for (int step = 0; step < i; ++step)
                        output.Write(" ");
                    output.Write("Property Sheet " + sheet.Name + "\n");
                    if (sheet.PropertySheets != null)
                        listPropertySheetCollection(output, sheet.PropertySheets, i+1);
                    if (sheet.Tools != null)
                        listPropertySheetCollection(output, sheet.Tools, i + 1);
                }
            }
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
            var mySettings = new Settings<PropertyPage.PropertyPage>(package);
            var compileCommand = mySettings.GetPage().CompileCommand;
            if (compileCommand == null || compileCommand.Length == 0)
            {
                TidyControl.CancelAndShowHelp(package);
                return;
            }
            CLangHelpers.CreateCompilationDatabase(package, compileCommand);
         }
    }
}
