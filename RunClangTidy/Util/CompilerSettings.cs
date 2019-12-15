using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.VCProjectEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace CodingToolBox.Util
{
    public class CompilerSettings
    {
        public const string NO_INHERIT = "$(NOINHERIT)";
        public const string CLCOMPILE = "ClCompile";
        private List<string> forcedIncludes = new List<string>();
        private List<string> additionalIncludes = new List<string>();
        private List<string> preprocessorDefinitions = new List<string>();
        private HashSet<string> fullIncludes = new HashSet<string>();
            
        public List<string> PreprocessorDefinitions => preprocessorDefinitions;
        public List<string> AdditionalIncludeDirectories => additionalIncludes;
        public List<string> ForcedIncludeFiles => forcedIncludes;

        public HashSet<string> FullIncludes => fullIncludes;

        public string compileAs { get; set; }

        bool InheritPreprocessorDefinitions
        {
            get { return PreprocessorDefinitions.Count == 0 || !preprocessorDefinitions.Contains(NO_INHERIT); }
        }

        bool InheritAdditionalIncludeDirectories
        {
            get { return AdditionalIncludeDirectories.Count == 0|| !AdditionalIncludeDirectories.Contains(NO_INHERIT); }
        }

        bool InheritForcedIncludeFiles
        {
            get { return ForcedIncludeFiles.Count == 0 || !ForcedIncludeFiles.Contains(NO_INHERIT); }
        }

        public CompilerSettings(CppProject project, ProjectItem prjItem)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var vcProject = prjItem.ContainingProject.Object as VCProject;
            var file = prjItem.Object;
            var activeSetting = project.GetActiveConfiguration(prjItem.ContainingProject);

            AddSettingsFromVCFile(this, file, activeSetting);
            AddSettingsFromVCProject(this, vcProject, activeSetting);
            
            if (FullIncludes.Count > 0)
            {
                var newList = CppProject.ExpandMacros(vcProject, activeSetting, FullIncludes.Aggregate((e, e1) => e + ";" + e1)).Split(';').ToList();
                FullIncludes.Clear();
                foreach (var x in newList)
                    if (x.Length > 0) FullIncludes.Add(x);
            }

            if (AdditionalIncludeDirectories.Count > 0)
            {
                var newList = CppProject.ExpandMacros(vcProject, activeSetting, AdditionalIncludeDirectories.Aggregate((e, e1) => e + ";" + e1)).Split(';').ToList();
                AdditionalIncludeDirectories.Clear();
                foreach (var x in newList)
                    if (x.Length > 0) AdditionalIncludeDirectories.Add(x);
            }

            if (PreprocessorDefinitions.Count > 0)
            {
                var newList = CppProject.ExpandMacros(vcProject, activeSetting, PreprocessorDefinitions.Aggregate((e, e1) => e + ";" + e1)).Split(';').ToList();
                PreprocessorDefinitions.Clear();
                foreach (var x in newList)
                {
                    var t = x.Trim();
                    if (t == "\\\"\\\"") continue;
                    if (t.Length > 0) PreprocessorDefinitions.Add(t);
                }
                    
            }

            if (ForcedIncludeFiles.Count > 0)
            {
                var newList = CppProject.ExpandMacros(vcProject, activeSetting, ForcedIncludeFiles.Aggregate((e, e1) => e + ";" + e1)).Split(';').ToList();
                ForcedIncludeFiles.Clear();
                ForcedIncludeFiles.AddRange(newList);
            }
        }

        private static void AddSettingsForCompilerTool(CompilerSettings settings, dynamic compilerTool)
        {
            settings.compileAs = "" + compilerTool.CompileAs;
            string fullInclude = compilerTool.FullIncludePath;
            foreach (var entry in fullInclude.Split(';'))
                settings.FullIncludes.Add(entry);

            string prePro = compilerTool.PreprocessorDefinitions;
            if (prePro != null && settings.InheritPreprocessorDefinitions && prePro.Length > 0)
                settings.PreprocessorDefinitions.Add(prePro);

            string additionalDirs = compilerTool.AdditionalIncludeDirectories;
            if (additionalDirs != null && settings.InheritAdditionalIncludeDirectories && additionalDirs.Length > 0)
                settings.AdditionalIncludeDirectories.Add(additionalDirs);

            string forcedIncludes = compilerTool.ForcedIncludeFiles;
            if (forcedIncludes != null && settings.InheritForcedIncludeFiles && forcedIncludes.Length > 0)
                settings.ForcedIncludeFiles.Add(forcedIncludes);
        }
        private static void AddSettingsFromVCProject(CompilerSettings settings, dynamic Project, string ActiveSetting)
        {
            dynamic CollectionOfConfigurations = Project.Configurations;
            dynamic Configuration = CollectionOfConfigurations.Item(ActiveSetting);
            dynamic Tools = Configuration.Tools;
            try
            {
                dynamic CompilerTool = Tools.Item("VCCLCompilerTool");
                if (CompilerTool != null) AddSettingsForCompilerTool(settings, CompilerTool);
            }
            catch {}

            dynamic CollectionOfPropertySheets = Configuration.PropertySheets;
            var SheetCount = CollectionOfPropertySheets.Count;
            for (int i = 0; i < SheetCount; i++)
            {
                try
                {
                    dynamic PropertySheet = CollectionOfPropertySheets.Item(i + 1);
                    dynamic CollectionOfTools = PropertySheet.Tools;
                    dynamic CompilerTool = CollectionOfTools.Item("VCCLCompilerTool");
                    if (CompilerTool != null) AddSettingsForCompilerTool(settings, CompilerTool);
                }
                catch { }
            }
        }

        private static void AddSettingsFromVCFile(CompilerSettings settings, dynamic File, string ActiveSetting)
        {
            dynamic CollectionOfFileConfigurations = File.FileConfigurations;
            dynamic FileConfiguration = CollectionOfFileConfigurations.Item(ActiveSetting);
            try
            {
                dynamic CompilerTool = FileConfiguration.Tool;
                if (CompilerTool != null) AddSettingsForCompilerTool(settings, CompilerTool);
            }
            catch { }
        }
     }
}

