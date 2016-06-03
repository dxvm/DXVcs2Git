using System;
using System.Runtime.Serialization;

namespace DXVcs2Git.Core.Git {
    [Serializable]
    public class ProcessException : Exception {
        public int ExitCode { get; private set; }

        public ProcessException()
            : this(-1, "An exception occurred in the process") {
        }

        public ProcessException(string message)
            : this(-1, message) {
        }

        public ProcessException(string message, Exception innerException)
            : this(-1, message, innerException) {
        }

        public ProcessException(int exitCode, string message)
            : this(exitCode, message, null) {
        }

        public ProcessException(int exitCode, string message, Exception innerException)
            : base(message, innerException) {
            this.ExitCode = exitCode;
        }

        protected ProcessException(SerializationInfo info, StreamingContext context)
            : base(info, context) {
            if (info == null)
                return;
            info.AddValue("ExitCode", this.ExitCode);
        }

        public override void GetObjectData(SerializationInfo info, StreamingContext context) {
            base.GetObjectData(info, context);
            info.AddValue("ExitCode", this.ExitCode);
        }
    }

    [Serializable]
    public class ProcessTimeoutException : ProcessException {
        public int TimeoutInMilliseconds { get; private set; }

        public ProcessTimeoutException()
            : this(-1) {
        }

        public ProcessTimeoutException(string message)
            : this(-1, message) {
        }

        public ProcessTimeoutException(string message, Exception innerException)
            : this(-1, message, innerException) {
        }

        public ProcessTimeoutException(int timeoutInMilliseconds)
            : this(timeoutInMilliseconds, "Process timed out") {
        }

        public ProcessTimeoutException(int timeoutInMilliseconds, string message)
            : this(timeoutInMilliseconds, message, null) {
        }

        public ProcessTimeoutException(int timeoutInMilliseconds, string message, Exception innerException)
            : base(-1, message, innerException) {
            this.TimeoutInMilliseconds = timeoutInMilliseconds;
        }

        protected ProcessTimeoutException(SerializationInfo info, StreamingContext context)
            : base(info, context) {
            if (info == null)
                return;
            info.AddValue("TimeoutInMilliseconds", this.TimeoutInMilliseconds);
        }

        public override void GetObjectData(SerializationInfo info, StreamingContext context) {
            base.GetObjectData(info, context);
            info.AddValue("TimeoutInMilliseconds", this.TimeoutInMilliseconds);
        }
    }
}