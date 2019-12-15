using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using VsUtil.Util;

namespace CodingToolBox.Util
{
    public class ExtensionOutput
    {
        public static readonly Guid DefaultOutputWindowGuid = new Guid("E5A0ABAB-9FC8-42FE-977D-65011D36909A");
        public ExtensionOutput(
                    AsyncPackage package,
                    string name, 
                    Guid guid,
                    bool visible = true, bool clearWithSolution = false)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            IVsOutputWindow output =
                package.GetService<SVsOutputWindow, IVsOutputWindow>() as IVsOutputWindow;
            m_guid = guid;
            output.GetPane(ref m_guid, out m_pane);
            if (m_pane == null)
            {
                // Create a new pane.
                output.CreatePane(
                    ref m_guid,
                    name,
                    Convert.ToInt32(visible),
                    Convert.ToInt32(clearWithSolution));

                // Retrieve the new pane.
                output.GetPane(ref guid, out m_pane);
            }
        }

        public void Write(string str)
        {
            if (str == null)
                return;
            var lines = str.Split('\n');
            foreach (var line in lines)
                if (line.Length > 0)
                {
                    var error = AnalysisOutputParser.GetMessage(line);
                    if (error != null)
                        m_pane.OutputStringThreadSafe(error.ToErrorString()+ "\n");
                    else
                        m_pane.OutputStringThreadSafe(line + "\n");
                }
        }
  
        public void Delete(AsyncPackage package)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (m_pane != null)
            {
                m_pane = null;
                IVsOutputWindow output =
                      package.GetService<SVsOutputWindow, IVsOutputWindow>() as IVsOutputWindow;
                output.DeletePane(ref m_guid);
            }
        }

        private IVsOutputWindowPane m_pane;
        private Guid m_guid;
    }
}
