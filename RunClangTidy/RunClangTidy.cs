using System;
using System.ComponentModel.Composition;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using CodingToolBox.Service;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.TableManager;
using Task = System.Threading.Tasks.Task;

namespace CodingToolBox
{
    /// <summary>
    /// This is the class that implements the package exposed by this assembly.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The minimum requirement for a class to be considered a valid package for Visual Studio
    /// is to implement the IVsPackage interface and register itself with the shell.
    /// This package uses the helper classes defined inside the Managed Package Framework (MPF)
    /// to do it: it derives from the Package class that provides the implementation of the
    /// IVsPackage interface and uses the registration attributes defined in the framework to
    /// register itself and its components with the shell. These attributes tell the pkgdef creation
    /// utility what data to put into .pkgdef file.
    /// </para>
    /// <para>
    /// To get loaded into VS, the package must be referred by &lt;Asset Type="Microsoft.VisualStudio.VsPackage" ...&gt; in .vsixmanifest file.
    /// </para>
    /// </remarks>
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [Guid(RunClangTidy.PackageGuidString)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideOptionPageAttribute(typeof(PropertyPage.PropertyPage),
    "RunClangTidy", "ClangSettings", 106, 107, true, DescriptionResourceId = "108")]
    [ProvideService((typeof(SInvokeTidyService)), IsAsyncQueryable = true)]
    [ProvideAutoLoadAttribute(VSConstants.UICONTEXT.VCProject_string, PackageAutoLoadFlags.BackgroundLoad)]
    public sealed class RunClangTidy : AsyncPackage
    {
        public const string PackageGuidString = "b11612a5-8e35-45fc-b0a5-7462f3671087";
        public const string PropertyPageGuidString = "76539A5D-4B51-42D1-9163-0B04AB16D241";
        #region Package Members

        [Import]
        public IMessageGenerator m_analysisService;

        /// <summary>
        /// Initialization of the package; this method is called right after the package is sited, so this is the place
        /// where you can put all the initialization code that rely on services provided by VisualStudio.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token to monitor for initialization cancellation, which can occur when VS is shutting down.</param>
        /// <param name="progress">A provider for progress updates.</param>
        /// <returns>A task representing the async work of package initialization, or an already completed task if there is none. Do not return null from this method.</returns>
        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            this.SatisfyImportsOnce(); // This calls the extension method
            await base.InitializeAsync(cancellationToken, progress);
            this.AddService(typeof(SInvokeTidyService), CreateTidyServiceAsync);
           
            // When initialized asynchronously, the current thread may be a background thread at this point.
            // Do any initialization that requires the UI thread after switching to the UI thread.
            await this.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            await global::CodingToolBox.Command.GenerateCompileDatabase.InitializeAsync(this);
            await global::CodingToolBox.Command.RunTidy.InitializeAsync(this);
            await global::CodingToolBox.Command.RunTidyFile.InitializeAsync(this);
            
        }

        public async Task<object> CreateTidyServiceAsync(IAsyncServiceContainer container, CancellationToken cancellationToken, Type serviceType)
        {
            InvokeTidyService service = new InvokeTidyService(this);
            await service.InitializeAsync(cancellationToken);
            return service;
        }

          #endregion
    }

    public static class MefExtensions
    {
        private static IComponentModel _compositionService;

        public static void SatisfyImportsOnce(this object o)
        {
            if (_compositionService == null)
            {
                _compositionService = ServiceProvider.GlobalProvider.GetService(typeof(SComponentModel)) as IComponentModel;
            }

            if (_compositionService != null)
            {
                _compositionService.DefaultCompositionService.SatisfyImportsOnce(o);
            }
        }
    }
}
