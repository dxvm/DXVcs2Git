using System.Diagnostics;

namespace DXVcs2Git.Core.Git {
    public interface IGitEnvironment {
        void Setup(string installDir);
        void SetUpEnvironment(ProcessStartInfo psi, string workingDirectory, bool internalUseOnly);
    }
}