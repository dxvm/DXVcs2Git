using System;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;

namespace DXVcs2Git.Core.Git {
    public static class NativeMethods {
        public const int IS_TEXT_UNICODE_UNICODE_MASK = 15;
        public const int IS_TEXT_UNICODE_REVERSE_MASK = 240;
        public const int IS_TEXT_UNICODE_NOT_UNICODE_MASK = 3840;
        public const int IS_TEXT_UNICODE_NOT_ASCII_MASK = 61440;
        public const int CP_ACP = 0;
        public const int CP_OEMCP = 1;
        public const int CP_MACCP = 2;
        public const int CP_THREAD_ACP = 3;
        public const int CP_SYMBOL = 42;
        public const int CP_UTF7 = 65000;
        public const int CP_UTF8 = 65001;

        [DllImport("advapi32", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsTextUnicode([MarshalAs(UnmanagedType.LPArray)] byte[] buffer, int cb, ref IsTextUnicodeFlags flags);

        [DllImport("kernel32", SetLastError = true)]
        public static extern int MultiByteToWideChar(uint codePage, MBWCFlags dwFlags, [MarshalAs(UnmanagedType.LPArray)] byte[] lpMultiByteStr, int cbMultiByte,
            [MarshalAs(UnmanagedType.LPWStr), Out] StringBuilder lpWideCharStr, int cchWideChar);

        [DllImport("advapi32")]
        public static extern int LsaNtStatusToWinError(int ntstatus);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ShowWindow(IntPtr hWnd, ShowWindowOption nCmdShow);

        [DllImport("kernel32.dll")]
        public static extern int RegisterApplicationRestart([MarshalAs(UnmanagedType.BStr)] string commandLineArgs, RestartRestrictions flags);
    }

    [SuppressUnmanagedCodeSecurity]
    public static class UnsafeNativeMethods {
        [DllImport("Kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool AttachConsole(int processId);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool FreeConsole();

        [DllImport("ntdll.dll")]
        public static extern int NtQueryInformationProcess(IntPtr processHandle, int processInformationClass, ref PROCESS_BASIC_INFORMATION processInformation, int processInformationLength,
            out int returnLength);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern IntPtr LoadLibrary(string lpFileName);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool TerminateProcess(IntPtr hProcess, uint uExitCode);

        [DllImport("kernel32.dll")]
        public static extern int WerRegisterMemoryBlock(IntPtr pvAddress, uint dwSize);
    }

    [Flags]
    public enum MBWCFlags {
        MB_PRECOMPOSED = 1,
        MB_COMPOSITE = 2,
        MB_ERR_INVALID_CHARS = 8,
        MB_USEGLYPHCHARS = 4
    }

    public enum ShowWindowOption {
        Hide,
        ShowNormal,
        ShowMinimized,
        Maximized,
        ShowNoActivate,
        Show,
        ShowMinimize,
        ShowMinNoActive,
        ShowNotActivated,
        Restore,
        ShowDefault,
        ForceMinimize
    }

    [Flags]
    public enum RestartRestrictions {
        None = 0,
        NotOnCrash = 1,
        NotOnHang = 2,
        NotOnPatch = 4,
        NotOnReboot = 8
    }

    [Flags]
    public enum IsTextUnicodeFlags {
        IS_TEXT_UNICODE_REVERSE_ASCII16 = 16,
        IS_TEXT_UNICODE_STATISTICS = 2,
        IS_TEXT_UNICODE_REVERSE_STATISTICS = 32,
        IS_TEXT_UNICODE_CONTROLS = 4,
        IS_TEXT_UNICODE_REVERSE_CONTROLS = 64,
        IS_TEXT_UNICODE_SIGNATURE = 8,
        IS_TEXT_UNICODE_REVERSE_SIGNATURE = 128,
        IS_TEXT_UNICODE_ILLEGAL_CHARS = 256,
        IS_TEXT_UNICODE_ODD_LENGTH = 512,
        IS_TEXT_UNICODE_NULL_BYTES = 4096
    }
}