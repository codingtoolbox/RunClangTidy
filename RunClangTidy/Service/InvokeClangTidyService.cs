using CodingToolBox.Util;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Task = System.Threading.Tasks.Task;

namespace CodingToolBox.Service
{
    public class InvokeTidyService : SInvokeTidyService, IInvokeTidyService
    {
        public const int TidyInstances = 4;
        private Microsoft.VisualStudio.Shell.IAsyncServiceProvider asyncServiceProvider;

        public InvokeTidyService(Microsoft.VisualStudio.Shell.IAsyncServiceProvider provider)
        {
            // constructor should only be used for simple initialization
            // any usage of Visual Studio service, expensive background operations should happen in the
            // asynchronous InitializeAsync method for best performance
            asyncServiceProvider = provider;
        }

        public async System.Threading.Tasks.Task InitializeAsync(CancellationToken cancellationToken)
        {
            await TaskScheduler.Default;
            // do background operations that involve IO or other async methods

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            // query Visual Studio services on main thread unless they are documented as free threaded explicitly.
            // The reason for this is the final cast to service interface (such as IVsShell) may involve COM operations to add/release references.

            IVsShell vsShell = this.asyncServiceProvider.GetServiceAsync(typeof(SVsShell)) as IVsShell;
            // use Visual Studio services to continue initialization
        }

        private string Quote(string path)
        {
            if (path.Contains(" "))
                return "\"" + path + "\"";
            return path;
        }

        public async System.Threading.Tasks.Task ExecuteTidyAsync(
                ExecuteShellCommand executor,
                string tidyPath,
                string compileDatabasePath,
                string tidyOptions,
                List<string> files, CancellationToken cancel)
        {
            int parallelTasks = TidyInstances;
            List<Process> runningChecks = new List<Process>();
            var fileArray = files.ToArray();
            int offset = 0;
            while (offset < fileArray.Length )
            {
                int limit = Math.Min(parallelTasks, fileArray.Length - offset);
                for (int i = 0;i < limit; ++i, ++offset)
                {
                    var process = executor.ExecuteCommand(Quote(tidyPath), "-p " + Quote(compileDatabasePath) + " " + tidyOptions + " " + Quote(fileArray[offset]));
                    if (process == null)
                        return;
                    runningChecks.Add(process);
                }
                try
                {
                    await Task.WhenAny(runningChecks.Select(x =>x.WaitForExitAsync(cancel)));
                    if (cancel.IsCancellationRequested)
                        throw new TaskCanceledException();
                    var running = runningChecks.Where(p => !p.HasExited);
                    runningChecks = new List<Process>(running);
                    parallelTasks = TidyInstances - runningChecks.Count;
                }
                catch (TaskCanceledException )
                {
                    executor.Cancel();
                    foreach (var task in runningChecks)
                        task.Kill();
                    return;
                }
            }
            await Task.WhenAll(runningChecks.Select(x => x.WaitForExitAsync(cancel)));
        }
    }

    public interface SInvokeTidyService
    {
    }

    public interface IInvokeTidyService
    {
        System.Threading.Tasks.Task ExecuteTidyAsync(
            ExecuteShellCommand executor,
            string tidyPath,
            string compileDatabasePath,
            string tidyOptions,
            List<string> files, 
            CancellationToken cancel);
    }
}
