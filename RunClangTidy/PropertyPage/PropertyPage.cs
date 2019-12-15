using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Drawing.Design;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace CodingToolBox.PropertyPage
{
    [Guid(RunClangTidy.PropertyPageGuidString)]
    public class PropertyPage : DialogPage
    {
        [Category("clang-tidy")]
        [DisplayName("clang-tidy location")]
        [Description("clang-tidy location")]
        [EditorAttribute(typeof(System.Windows.Forms.Design.FileNameEditor), typeof(System.Drawing.Design.UITypeEditor))]
        public string CLangTidy { get; set; }

        [Category("clang-tidy")]
        [DisplayName("clang-tidy options")]
        [Description("clang-tidy command line options")]
        [Editor(typeof(MultilineStringEditor), typeof(UITypeEditor))]
        public string AnalysisOptions { get; set; }

        [Category("clang-tidy")]
        [DisplayName("compile command")]
        [Description("clang compile command used for compilation database")]
        [Editor(typeof(MultilineStringEditor), typeof(UITypeEditor))]
        public string CompileCommand { get; set; } = "clang-tool";

    }
}
