using System;
using System.Runtime.InteropServices;

namespace DXVcs2Git.Core.Git {
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct PROCESS_BASIC_INFORMATION {
        public readonly IntPtr ExitStatus;
        public readonly IntPtr PebBaseAddress;
        public readonly IntPtr AffinityMask;
        public readonly IntPtr BasePriority;
        public readonly UIntPtr UniqueProcessId;
        public readonly IntPtr InheritedFromUniqueProcessId;
    }
}