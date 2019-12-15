using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System.Threading;

namespace CodingToolBox.Command
{
    class TidyControl
    {
        public static CancellationTokenSource RunningTidy {get; set;}

        public static void CancelAndShowHelp(AsyncPackage package)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            VsShellUtilities.ShowMessageBox(
                package,
                Resources.NeedToConfigure,
                Resources.Configuration,
                OLEMSGICON.OLEMSGICON_WARNING,
                OLEMSGBUTTON.OLEMSGBUTTON_OK,
                OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
            if (RunningTidy != null)
            {
                RunningTidy.Dispose();
                RunningTidy = null;
            }
            var dteService = package.GetService<EnvDTE.DTE, EnvDTE.DTE>();
            dteService.ExecuteCommand("Tools.Options", CommandArgs: RunClangTidy.PropertyPageGuidString);
        }


    }
}
