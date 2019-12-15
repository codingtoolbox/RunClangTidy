using Microsoft.VisualStudio.Shell;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using VsUtil.Util;

namespace CodingToolBox.Util
{

    public class ExecuteShellCommand
    {
        private readonly ExtensionOutput m_output;
        private AnalysisOutputParser m_errorProvider;
        public List<MessageText> Failures {get;set; }
        public ExecuteShellCommand(ExtensionOutput output, AnalysisOutputParser p)
        {
            m_output = output;
            m_errorProvider = p;
        }

        public void Done()
        {
            m_errorProvider.WaitCompletion();
            Failures = m_errorProvider.Failures;
            m_output.Write("Done.");
        }

        public Process ExecuteCommand(string command, string arguments)
        {
            var process = new Process();
            process.StartInfo.FileName = command;
            process.StartInfo.Arguments = arguments;

            m_output.Write("Starting: " + command + " " + arguments + "\n");
            
            string workingDirectory = null;
            try
            {
                workingDirectory = new FileInfo(process.StartInfo.FileName).Directory.FullName;
            }
            catch { }

            string processOutput = "";
            process.StartInfo.WorkingDirectory = workingDirectory;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.OutputDataReceived += (s, e) => {
                var line = e.Data + "\n";
                m_output.Write(line);
                processOutput += line;
                if (e.Data == null)
                    m_errorProvider.ParseOutput(processOutput);
            };
            process.ErrorDataReceived += (s, e) => {
                var line = e.Data + "\n";
                m_output.Write(line);
                processOutput += line;
            };
            process.EnableRaisingEvents = true;
            try
            {
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                m_output.Write(command + " " + arguments + "\n");
            }
            catch 
            {
                m_errorProvider.OutputComplete();
                m_output.Write("Failed to start: " + command + " " + arguments + "\n");
                return null;
            }
            return process;
        }

        public void Cancel()
        {
            m_errorProvider.Cancel();
        }
    }
}
