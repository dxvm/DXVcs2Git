using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace DXVcs2Git.Core.Git {
    public class PipableObservableProcess : IPipableObservableProcess, IObservable<Unit> {
        const int processTimeoutMilliseconds = 20000;
        readonly AsyncSubject<Unit> exit = new AsyncSubject<Unit>();
        readonly ProcessWrapper process;

        public PipableObservableProcess(ProcessStartInfo startInfo, bool throwOnNonZeroExitCode = true)
            : this(new ProcessWrapper(startInfo), throwOnNonZeroExitCode) {
        }

        public PipableObservableProcess(ProcessWrapper process, bool throwOnNonZeroExitCode) {
            PipableObservableProcess observableProcess = this;
            this.process = process;
            try {
                process.Start();
                Observable.Start(() => observableProcess.WaitForExit(throwOnNonZeroExitCode));
            }
            catch (Exception ex) {
                this.exit.OnError(ex);
                this.Cleanup();
            }
        }

        public Stream StandardInput {
            get { return this.process.StandardInput.BaseStream; }
        }
        public Stream StandardOutput {
            get { return this.process.StandardOutput.BaseStream; }
        }

        public IDisposable Subscribe(IObserver<Unit> observer) {
            return this.exit.Subscribe(observer);
        }

        void WaitForExit(bool throwOnNonZeroExitCode) {
            try {
                var completed = false;
                if (this.GetExitCode(ref completed) != 0 && throwOnNonZeroExitCode)
                    this.exit.OnError(GetExceptionFromStandardError(this.process, completed));
                else {
                    this.exit.OnNext(Unit.Default);
                    this.exit.OnCompleted();
                }
            }
            catch (Exception ex) {
                LogProcessError("Failed to Observer Process", ex, this.process);
                if (!ex.IsCriticalException())
                    return;
                throw;
            }
            finally {
                this.Cleanup();
            }
        }

        int GetExitCode(ref bool completed) {
            if (GetResultOrWarnException(() => this.process.WaitForExit(processTimeoutMilliseconds), false, "Failed to wait for exit: " + this.process.GetProcessLogInfo())) {
                completed = true;
                return this.process.ExitCode;
            }
            this.TryKillApplication();
            return -1;
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
        void TryKillApplication() {
            Log.DoOrWarnException(this.process.KillProcessTree, "Failed to kill the process tree." + this.process.GetProcessLogInfo());
            this.exit.OnError(new ProcessTimeoutException("Process timed out"));
        }
        void Cleanup() {
            try {
                this.process.Close();
            }
            catch (Exception ex) {
                Log.Error(string.Format(CultureInfo.InvariantCulture, "Error closing the process '{0}'", new object[1] {this.process.GetProcessLogInfo()}), ex);
                if (!ex.IsCriticalException())
                    return;
                throw;
            }
        }

        static void LogProcessError(string message, Exception exception, ProcessWrapper process) {
            string message1 = string.Format(CultureInfo.InvariantCulture, "ASSERT: " + message + " " + process.GetProcessLogInfo());
            Log.Error(message1, exception);
        }

        static ProcessException GetExceptionFromStandardError(ProcessWrapper process, bool completed) {
            return completed ? new ProcessException(process.ExitCode, process.StandardError.ReadToEnd()) : new ProcessTimeoutException(3600000);
        }
    }
}