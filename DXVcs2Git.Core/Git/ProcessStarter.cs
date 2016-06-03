using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace DXVcs2Git.Core.Git {
    public class ProcessStarter {
        public ProcessWrapper Start(ProcessStartInfo processStartInfo) {
            return new ProcessWrapper(processStartInfo, true);
        }

        public ProcessWrapper Start(string fileName, string arguments) {
            Process process = Process.Start(fileName, arguments);
            if (process == null && (fileName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || !string.IsNullOrEmpty(arguments)))
                throw new InvalidOperationException("Failed to start executable");
            return new ProcessWrapper(process);
        }

        public ProcessWrapper OpenToEdit(string path) {
            path = new FileInfo(path).FullName;
            ProcessStartInfo processStartInfo = new ProcessStartInfo(path) {
                ErrorDialog = false,
                Verb = "Edit",
                UseShellExecute = true
            };
            try {
                return this.Start(processStartInfo);
            }
            catch (Win32Exception) {
                return this.Start(new ProcessStartInfo(isSafeFile(path) ? path : Path.GetDirectoryName(path)) {
                    ErrorDialog = false,
                    UseShellExecute = true
                });
            }
        }

        static bool isSafeFile(string path) {
            var strArray = new string[9] {
                ".sln",
                ".csproj",
                ".vbproj",
                ".fsproj",
                ".gif",
                ".png",
                ".jpg",
                ".jpeg",
                ".bmp"
            };
            string ext = Path.GetExtension(path);
            return strArray.Any(x => x.Equals(ext, StringComparison.OrdinalIgnoreCase));
        }

        public IObservableProcess StartObservableProcess(ProcessStartInfo startInfo, bool throwOnNonZeroExitCode) {
            return new ObservableProcess(startInfo, throwOnNonZeroExitCode);
        }

        public IPipableObservableProcess StartObservablePipedProcess(ProcessStartInfo startInfo, bool throwOnNonZeroExitCode) {
            return new PipableObservableProcess(startInfo, throwOnNonZeroExitCode);
        }
    }
}