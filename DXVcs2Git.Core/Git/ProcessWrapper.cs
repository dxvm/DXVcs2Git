using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace DXVcs2Git.Core.Git {
    public class ProcessWrapper : IDisposable {
        public int Id {
            get { return this.process.Id; }
        }

        public ProcessStartInfo StartInfo {
            get { return this.process.StartInfo; }
            set { this.process.StartInfo = value; }
        }

        public StreamWriter StandardInput {
            get { return this.process.StandardInput; }
        }

        public StreamReader StandardOutput {
            get { return this.process.StandardOutput; }
        }

        public StreamReader StandardError {
            get { return this.process.StandardError; }
        }

        public int ExitCode {
            get { return this.process.ExitCode; }
        }

        public IntPtr MainWindowHandle {
            get { return this.process.MainWindowHandle; }
        }

        public string ProcessName {
            get { return this.process.ProcessName; }
        }
        readonly Process process;
        readonly SafeDisposable processDisposer;

        public ProcessWrapper(ProcessStartInfo startInfo)
            : this(startInfo, false) {
        }

        public ProcessWrapper(ProcessStartInfo startInfo, bool startImmediately)
            : this(CreateProcess(startInfo), startImmediately) {
        }

        public ProcessWrapper(Process process)
            : this(process, false) {
        }

        protected ProcessWrapper(Process process, bool startImmediately) {
            this.process = process;
            process.OutputDataReceived += this.OnOutputDataReceived;
            process.ErrorDataReceived += this.OnErrorDataReceived;
            this.processDisposer = new SafeDisposable(process);
            if (!startImmediately)
                return;
            this.Start();
        }

        public void Dispose() {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        public event DataReceivedEventHandler OutputDataReceived;

        public event DataReceivedEventHandler ErrorDataReceived;

        static Process CreateProcess(ProcessStartInfo startInfo) {
            return new Process {
                StartInfo = startInfo,
                EnableRaisingEvents = true
            };
        }

        void OnOutputDataReceived(object sender, DataReceivedEventArgs e) {
            this.OnDataReceived(this.OutputDataReceived, e);
        }

        void OnErrorDataReceived(object sender, DataReceivedEventArgs e) {
            this.OnDataReceived(this.ErrorDataReceived, e);
        }

        void OnDataReceived(DataReceivedEventHandler handler, DataReceivedEventArgs e) {
            if (handler == null)
                return;
            handler(this, e);
        }

        public void BeginOutputReadLine() {
            this.process.BeginOutputReadLine();
        }

        public void BeginErrorReadLine() {
            this.process.BeginErrorReadLine();
        }

        public bool Start() {
            try {
                Log.Message("Starting: "
                            + GetProcessLogInfo());
                return this.process.Start();
            }
            catch (Exception ex) {
                Log.Error(string.Format(CultureInfo.InvariantCulture, "Exception thrown while running the following command: '{0} {1}'",
                    this.process.StartInfo.FileName, this.process.StartInfo.Arguments, ex));
                throw;
            }
        }
        public string GetProcessLogInfo() {
            return string.Format(CultureInfo.InvariantCulture, "Process - FileName: '{0}', Args: '{1}', Working Directory: {2}",
                SafeGet(() => this.process.StartInfo.FileName),
                SafeGet(() => this.process.StartInfo.Arguments),
                SafeGet(() => this.process.StartInfo.WorkingDirectory));
        }
        object SafeGet<T>(Func<T> func) {
            try {
                return func().ToString();
            }
            catch (Exception) {
                return "(unknown)";
            }
        }

        public bool WaitForExit(int milliseconds) {
            bool flag = this.process.WaitForExit(milliseconds);
            if (flag)
                this.process.WaitForExit();
            return flag;
        }

        public void Close() {
            this.process.Close();
        }

        public void Kill() {
            this.process.Kill();
        }

        public void KillProcessTree() {
            KillProcessTree(this.process);
        }

        void KillProcessTree(Process process) {
            foreach (Process process1 in GetChildProcesses(process.Id))
                KillProcessTree(process1);
            Log.DoOrWarnException(process.Kill, "Failed to kill a process");
        }

        public void Show() {
        }

        protected virtual void Dispose(bool disposing) {
            if (!disposing)
                return;
            this.processDisposer.Dispose();
        }

        static IEnumerable<Process> GetChildProcesses(int processId) {
            try {
                return Process.GetProcesses().Select(process => new {
                    process,
                    parent = GetResultOrWarnException(() => GetParentProcess(process), null, "Failed to get parent process")
                }).Where(param0 => {
                    if (param0.parent != null)
                        return param0.parent.Id == processId;
                    return false;
                }).Select(param0 => param0.process);
            }
            catch (Exception ex) {
                Log.Error("Exception while enumerating child processes", ex);
                throw;
            }
        }
        public static T GetResultOrWarnException<T>(Func<T> action, T defaultValue, string failureMessage, params object[] args) {
            try {
                return action();
            }
            catch (Exception ex) {
                Log.Message(string.Format(CultureInfo.InvariantCulture, failureMessage, args), ex);
                return defaultValue;
            }
        }

        static Process GetParentProcess(Process process) {
            try {
                return GetParentProcess(process.Handle);
            }
            catch (Win32Exception ex) {
                if (ex.NativeErrorCode == 5)
                    return null;
                throw;
            }
        }

        static Process GetParentProcess(IntPtr handle) {
            PROCESS_BASIC_INFORMATION processInformation = new PROCESS_BASIC_INFORMATION();
            int returnLength;
            int error =
                NativeMethods.LsaNtStatusToWinError(UnsafeNativeMethods.NtQueryInformationProcess(handle, 0, ref processInformation, Marshal.SizeOf((object)processInformation), out returnLength));
            if (error != 0)
                throw new Win32Exception(error);
            try {
                return Process.GetProcessById(processInformation.InheritedFromUniqueProcessId.ToInt32());
            }
            catch (ArgumentException) {
                return null;
            }
        }
    }
}