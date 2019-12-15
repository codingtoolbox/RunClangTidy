using Microsoft.VisualStudio.Settings;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Settings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CodingToolBox.Util
{
    public class Settings<T> where T : class
    {
        private AsyncPackage m_package;

        public Settings(AsyncPackage package)
        {
            m_package = package;
        }

        public T GetPage()
        {
            return m_package.GetDialogPage(typeof(T)) as T;
        }
    }
}
