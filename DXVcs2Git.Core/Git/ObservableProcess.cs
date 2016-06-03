using System;
using System.Diagnostics;
using System.Globalization;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Threading.Tasks;

namespace DXVcs2Git.Core.Git {
    public class ObservableProcess : IObservableProcess {
        const int processTimeoutMilliseconds = 3600000;
        readonly ReplaySubject<string> combinedOutput = new ReplaySubject<string>();
        readonly ReplaySubject<string> error = new ReplaySubject<string>();
        readonly AsyncSubject<Unit> exit = new AsyncSubject<Unit>();
        readonly object gate = 42;
        readonly ReplaySubject<string> output = new ReplaySubject<string>();

        public ObservableProcess(ProcessStartInfo startInfo, bool throwOnNonZeroExitCode = true)
            : this(new ProcessWrapper(startInfo), throwOnNonZeroExitCode) {
        }

        public ObservableProcess(ProcessWrapper process, bool throwOnNonZeroExitCode) {
            ObservableProcess observableProcess = this;
            process.OutputDataReceived += this.OnReceived;
            process.ErrorDataReceived += this.OnErrorReceived;
            try {
                process.Start();
            }
            catch (Exception ex) {
                this.error.OnNext(ex.Message);
                this.exit.OnError(ex);
                this.Input = new ReplaySubject<string>();
                this.Cleanup(process);
                return;
            }
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            Input = Observer.Create<string>(x => {
                try {
                    process.StandardInput.WriteLine(x);
                    process.StandardInput.Flush();
                }
                catch (Exception ex) {
                    Log.Error(@"Error occurred while trying to write to the standard input", ex);
                    if (ex.IsCriticalException())
                        throw;
                    LogProcessError(process.GetProcessLogInfo(), ex, process);
                }
            }, () => { });
            Observable.Start(() => observableProcess.ObserveProcess(process, throwOnNonZeroExitCode));
        }

        public IObserver<string> Input { get; }

        public IObservable<string> CombinedOutput {
            get { return this.combinedOutput; }
        }

        public IObservable<string> Output {
            get { return this.output; }
        }

        public IObservable<string> Error {
            get { return this.error; }
        }

        void ObserveProcess(ProcessWrapper process, bool throwOnNonZeroExitCode) {
            try {
                var completed = false;
                int exitCode = this.GetExitCode(process, ref completed);
                if (exitCode != 0 && throwOnNonZeroExitCode) {
                    this.exit.OnError(completed
                        ? new ProcessException(exitCode, string.Join("\n", this.combinedOutput.ToArray().ToTask().Result))
                        : new ProcessTimeoutException(3600000));
                }
                else {
                    this.exit.OnNext(Unit.Default);
                    this.exit.OnCompleted();
                }
            }
            catch (Exception ex) {
                LogProcessError("Failed to Observer Process", ex, process);
                throw;
            }
        }

        int GetExitCode(ProcessWrapper process, ref bool completed) {
            try {
                if (GetResultOrWarnException(() => process.WaitForExit(processTimeoutMilliseconds), false, "Failed to wait for exit: " + process.GetProcessLogInfo())) {
                    completed = true;
                    return process.ExitCode;
                }
                Log.DoOrWarnException(process.KillProcessTree, "Failed to kill the process tree." + process.GetProcessLogInfo());
                this.error.OnNext("Process timed out");
                return -1;
            }
            finally {
                this.Cleanup(process);
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
        void Cleanup(ProcessWrapper process) {
            this.output.OnCompleted();
            this.error.OnCompleted();
            this.combinedOutput.OnCompleted();
            process.OutputDataReceived -= this.OnReceived;
            process.ErrorDataReceived -= this.OnReceived;
            try {
                process.Close();
            }
            catch (Exception ex) {
                if (!ex.IsCriticalException())
                    return;
                Log.Error(string.Format(CultureInfo.InvariantCulture, "ASSERT! Error closing the process '{0}'", new object[] {process.GetProcessLogInfo()}), ex);
                throw;
            }
        }

        public IDisposable Subscribe(IObserver<Unit> observer) {
            return this.exit.Subscribe(observer);
        }

        void OnReceived(object s, DataReceivedEventArgs e) {
            if (e.Data == null)
                return;
            this.NotifyData(this.output, e.Data);
        }

        void OnErrorReceived(object s, DataReceivedEventArgs e) {
            if (e.Data == null)
                return;
            this.NotifyData(this.error, e.Data);
        }

        void NotifyData(IObserver<string> observable, string data) {
            lock (this.gate) {
                observable.OnNext(data);
                this.combinedOutput.OnNext(data);
            }
        }

        static void LogProcessError(string message, Exception exception, ProcessWrapper process) {
            string message1 = string.Format(CultureInfo.InvariantCulture, "ASSERT: " + message + " " + process.GetProcessLogInfo());
            Log.Error(message1, exception);
        }
    }
}