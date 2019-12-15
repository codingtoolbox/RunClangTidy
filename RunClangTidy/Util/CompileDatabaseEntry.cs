using Microsoft.VisualStudio.VCProjectEngine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CodingToolBox.Util
{
    class CompileDatabaseEntry
    {
        public string directory { get; set; }
        public string file { get; set; }
        public string command { get; set; }

        public CompileDatabaseEntry(VCFile f, CompilerSettings settings,  string compileCommand)
        {
            file = f.FullPath;
            directory = new FileInfo(file).DirectoryName;
            command = compileCommand;
            foreach (var include in settings.FullIncludes)
                command += " -I\"" + include.TrimEnd('\\').TrimStart('\\') + "\"";
            
            foreach (var def in settings.PreprocessorDefinitions)
                command += " -D " + def;
            command += " \"" + file + "\"";
        }
    }
}
