using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.VCProjectEngine;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CodingToolBox.Util
{
    public interface ProjectItemVisitor
    {
        void EnterProjectItem(ProjectItem item);
        void LeaveProjectItem(ProjectItem item);
    }

    class CompilationDatabaseVisitor : ProjectItemVisitor
    {
        private readonly List<CompileDatabaseEntry> compileDatabase = new List<CompileDatabaseEntry>();
        public List<CompileDatabaseEntry> CompileDatabase => compileDatabase;
        private readonly ExtensionOutput m_output;
        private readonly CppProject m_project;
        private readonly string m_compileCommand;
        public CompilationDatabaseVisitor(ExtensionOutput output, CppProject project, string compileCommand)
        {
            m_output = output;
            m_project = project;
            m_compileCommand = compileCommand;
        }
        public void EnterProjectItem(ProjectItem item)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var vcFile = item.Object as VCFile;
            if (vcFile == null)
                return;

            if (vcFile.ItemType != CompilerSettings.CLCOMPILE)
                return;
            CompilerSettings settings = new CompilerSettings(m_project, item);
            CompileDatabaseEntry e = new CompileDatabaseEntry(vcFile, settings, m_compileCommand);
            CompileDatabase.Add(e);
        }

        public void LeaveProjectItem(ProjectItem item)
        {
        }
    };
    
    public class CppProject
    {
        public CppProject(EnvDTE.DTE dte)
        {
            m_dte = dte;
        }

        public Projects GetVCProjects()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            return m_dte.GetObject("VCProjects") as Projects;
        }

        public Project GetProjectFromSelected()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var items = m_dte.SelectedItems;
            if (items.Count == 1)
            {
                var project = items.Item(1).Project;
                if (project != null)
                    return project;

                var projectItem = items.Item(1).ProjectItem;
                return projectItem.ContainingProject;
            }
            return null;
        }

        public List<(VCFile, Project)> GetVCFilesFromSelected()
        {
            List<(VCFile, Project)> result = new List<(VCFile, Project)>();
            ThreadHelper.ThrowIfNotOnUIThread();
            var items = m_dte.SelectedItems;
            for (int i = 1; i <=items.Count; ++i)
            {
                var item = items.Item(i);
                var projectItem = item.ProjectItem;
                var vcFile = projectItem.Object as VCFile;
                if (vcFile == null)
                    continue;

                if (vcFile.ItemType != CompilerSettings.CLCOMPILE)
                    continue;

                result.Add((vcFile, projectItem.ContainingProject));
            }
            return result;
        }

        public string GetActiveConfiguration(Project project)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            ConfigurationManager cm = project.ConfigurationManager;
            if (cm == null)
                return null;
            Configuration conf = cm.ActiveConfiguration;
            if (conf == null)
                return null;
            String platformName = conf.PlatformName;
            String configName = conf.ConfigurationName;
            String pattern = configName + "|" + platformName;
            return pattern;
        }

        public void Save(Project project)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            project.Save();
        }

        public IVsHierarchy ToHierarchy(EnvDTE.Project project)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            System.IServiceProvider serviceProvider =
              new ServiceProvider(project.DTE as
            Microsoft.VisualStudio.OLE.Interop.IServiceProvider);
            Guid guid = GetProjectGuid(serviceProvider, project);
            if (guid == Guid.Empty)
                return null;
            return VsShellUtilities.GetHierarchy(serviceProvider, guid);
        }

        public Guid GetProjectGuid(System.IServiceProvider serviceProvider, Project project)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            IVsSolution solution = serviceProvider.GetService(typeof(SVsSolution)) as IVsSolution;
            if (solution == null)
                return Guid.Empty;

            if (ProjectUnloaded(project))
                return Guid.Empty;

            IVsHierarchy hierarchy;
            solution.GetProjectOfUniqueName(project.FullName, out hierarchy);
            if (hierarchy != null)
            {
                Guid projectGuid;

                ErrorHandler.ThrowOnFailure(
                  hierarchy.GetGuidProperty(
                              VSConstants.VSITEMID_ROOT,
                              (int)__VSHPROPID.VSHPROPID_ProjectIDGuid,
                              out projectGuid));

                if (projectGuid != null)
                    return projectGuid;
            }

            return Guid.Empty;
        }

        private uint GetItemId(object pvar)
        {
            if (pvar == null) 
                return VSConstants.VSITEMID_NIL;
            if (pvar is int iRet) return (uint)iRet;
            if (pvar is uint uRet) return uRet;
            if (pvar is short sRet) return (uint)sRet;
            if (pvar is ushort usRet) return (uint)usRet;
            if (pvar is long lRet) return (uint)lRet;
            return VSConstants.VSITEMID_NIL;
        }

        public void VisitHierarchy(IVsHierarchy hierarchy, ProjectItemVisitor visitor, bool visibleNodesOnly = false, uint itemId = VSConstants.VSITEMID_ROOT)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (hierarchy == null)
                return;

            object pVar;
            var hr = hierarchy.GetProperty(itemId,
              (int)__VSHPROPID.VSHPROPID_ExtObject, out pVar);
            if (hr != VSConstants.S_OK)
                return;

            ProjectItem projectItem = pVar as ProjectItem;
            if (projectItem != null)
                visitor.EnterProjectItem(projectItem);
            
            hr = hierarchy.GetProperty(itemId,
              (visibleNodesOnly ? (int)__VSHPROPID.VSHPROPID_FirstVisibleChild
              : (int)__VSHPROPID.VSHPROPID_FirstChild),
              out pVar);
            Microsoft.VisualStudio.ErrorHandler.ThrowOnFailure(hr);
            if (hr != VSConstants.S_OK)
                return;

            var childId = GetItemId(pVar);
            while (childId != VSConstants.VSITEMID_NIL)
            {
                VisitHierarchy(hierarchy, visitor, visibleNodesOnly, childId);
                hr = hierarchy.GetProperty(childId,
                    (visibleNodesOnly ?
                    (int)__VSHPROPID.VSHPROPID_NextVisibleSibling :
                    (int)__VSHPROPID.VSHPROPID_NextSibling),
                    out pVar);
                if (hr != VSConstants.S_OK)
                {
                    Microsoft.VisualStudio.ErrorHandler.ThrowOnFailure(hr);
                    break;
                }

                childId = GetItemId(pVar);
            }
            visitor.LeaveProjectItem(projectItem);
        }

        public bool ProjectUnloaded(Project project)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            return string.Compare(EnvDTE.Constants.vsProjectKindUnmodeled, project.Kind, System.StringComparison.OrdinalIgnoreCase) == 0;
        }

        public static string ExpandMacros(VCProject Project, string ActiveConfiguration, string MacroToEvaluate)
        {
            dynamic CollectionOfConfigurations = Project.Configurations;
            //Extract Config from Collection, with Name stored in ActiveSetting
            dynamic Configuration = CollectionOfConfigurations.Item(ActiveConfiguration);
            return Configuration.Evaluate(MacroToEvaluate);
        }
        public static readonly string ProjectDir = "$(ProjectDir)";
        public static readonly string OutputDir = "$(OutputDir)";

        private EnvDTE.DTE m_dte;
    }

    public class CLangHelpers
    {
        public static void CreateCompilationDatabase(AsyncPackage package, string compileCommand)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var myOutput = new ExtensionOutput(package, "CLang Output", ExtensionOutput.DefaultOutputWindowGuid);
            var dteService = package.GetService<EnvDTE.DTE, EnvDTE.DTE>();
            var cppSupport = new CppProject(dteService);
            var project = cppSupport.GetProjectFromSelected();
            if (project == null)
                return;
            cppSupport.Save(project); // ensure that project is saved before we do anything
            var hierarchy = cppSupport.ToHierarchy(project as EnvDTE.Project);
            var visitor = new CompilationDatabaseVisitor(myOutput, cppSupport, compileCommand);
            cppSupport.VisitHierarchy(hierarchy, visitor);
            var directory = new FileInfo(project.FileName).DirectoryName;
            var outputFile = GetCompileDatabasePath(directory);
            string compileDatabase = JsonConvert.SerializeObject(visitor.CompileDatabase);
            File.WriteAllText(outputFile, compileDatabase);
        }

        public static string GetCompileDatabasePath(string dir)
        {
            return dir + @"\compile_commands.json";
        }

    }
}
