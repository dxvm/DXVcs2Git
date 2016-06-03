using System.Diagnostics;
using System.IO;

namespace DXVcs2Git.Core.Git {
    public class GitProcessManager {
        readonly ProcessStarter processManager;
        public GitProcessManager(ProcessStarter processManager) {
            this.processManager = processManager;
        }
        public IObservableProcess StartProcess(IGitEnvironment environment, string executableFileName, string arguments, string workingDirectory, bool throwOnNonZeroExitCode, bool ensureGitExtracted) {
            ProcessStartInfo startInfo = this.SetUpStartInfo(executableFileName, arguments, workingDirectory ?? Path.GetTempPath());
            environment.SetUpEnvironment(startInfo, workingDirectory, true);
            startInfo.FileName = executableFileName;
            return this.processManager.StartObservableProcess(startInfo, throwOnNonZeroExitCode);
        }

        public IPipableObservableProcess StartObservableRedirectedProcess(IGitEnvironment environment, string executableFileName, string arguments, string workingDirectory, bool throwOnNonZeroExitCode, bool ensureGitExtracted) {
            ProcessStartInfo startInfo = this.SetUpStartInfo(executableFileName, arguments, workingDirectory ?? Path.GetTempPath());
            environment.SetUpEnvironment(startInfo, workingDirectory, true);
            startInfo.FileName = executableFileName;
            return this.processManager.StartObservablePipedProcess(startInfo, throwOnNonZeroExitCode);
        }
        public ProcessStartInfo SetUpStartInfo(string executableFileName, string arguments, string workingDirectory) {
            return new ProcessStartInfo(executableFileName, arguments) {
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                FileName = executableFileName
            };
        }
    }
}