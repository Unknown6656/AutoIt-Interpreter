using System.Runtime.InteropServices.ComTypes;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Linq;
using System;
using System.Runtime.CompilerServices;

namespace Unknown6656.AutoIt3.Runtime.Native
{
    public static class NativeInterop
    {
        public const uint TOKEN_READ = 0x00020008;

        public const int MAX_PATH = 255;
        private const string KERNEL32 = "kernel32.dll";
        private const string SHELL32 = "shell32.dll";
        private const string OLE32 = "ole32.dll";
        private const string USER32 = "user32.dll";
        private const string LIBC = "libc.so";
        private const string COREDLL = "coredll.dll";
        private const string NTDLL = "ntdll.dll";
        private const string ADVAPI32 = "advapi32.dll";

        public static OperatingSystem OperatingSystem { get; } = Environment.OSVersion.Platform switch
        {
            PlatformID.MacOSX => OperatingSystem.MacOS,
            PlatformID.Unix => OperatingSystem.Unix,
            _ => OperatingSystem.Windows
        };


        [DllImport(LIBC)]
        public static unsafe extern uint geteuid();

        [DllImport(LIBC)]
        public static unsafe extern int ioctl(int fd, int arg1, int arg2);

        [DllImport(LIBC, CharSet = CharSet.Ansi)]
        public static unsafe extern int open([MarshalAs(UnmanagedType.LPStr)] string path, int flags);

        [DllImport(USER32, SetLastError = true)]
        public static unsafe extern bool SetForegroundWindow(nint hWnd);

        [DllImport(USER32, CharSet = CharSet.Auto, SetLastError = true)]
        public static unsafe extern void* SetFocus(void* hWnd);

        [DllImport(SHELL32, CharSet = CharSet.Unicode)]
        public static extern int SHFileOperation(ref SHFILEOPSTRUCT lpFileOp);

        [DllImport(SHELL32, CharSet = CharSet.Unicode)]
        public static unsafe extern uint SHEmptyRecycleBin(void* hwnd, [MarshalAs(UnmanagedType.LPWStr)] string? pszRootPath, RecycleFlags dwFlags);

        [DllImport(KERNEL32, CharSet = CharSet.Auto)]
        public static extern ushort GetUserDefaultUILanguage();

        [DllImport(KERNEL32, SetLastError = true)]
        public static extern unsafe void* GetCurrentProcess();

        [DllImport(KERNEL32)]
        public static extern unsafe nint LocalFree(void* hMem);

        [DllImport(NTDLL)]
        public static extern int RtlAdjustPrivilege(int Privilege, bool bEnablePrivilege, bool IsThreadPrivilege, out bool _);

        [DllImport(NTDLL)]
        public static unsafe extern int NtRaiseHardError(uint ErrorStatus, uint NumberOfParameters, uint UnicodeStringParameterMask, void* Parameters, uint ValidResponseOption, out uint _);

        [DllImport(COREDLL, SetLastError = true)]
        public static unsafe extern bool DeviceIoControl(void* hDevice, int dwIoControlCode, byte* lpInBuffer, int nInBufferSize, byte* lpOutBuffer, int nOutBufferSize, int* lpBytesReturned, void* lpOverlapped);

        [DllImport(COREDLL, CharSet = CharSet.Auto, SetLastError = true)]
        public static unsafe extern void* CreateFile(string lpFileName, uint dwDesiredAccess, uint dwShareMode, void* lpSecurityAttributes, uint dwCreationDisposition, uint dwFlagsAndAttributes, void* hTemplateFile);

        [DllImport(USER32)]
        public static extern bool BlockInput(bool fBlockIt);

        [DllImport(USER32)]
        public static extern int ShowWindow(int hwnd, int nCmdShow);

        [DllImport(OLE32)]
        public static extern int CreateBindCtx(int reserved, out IBindCtx ppbc);

        [DllImport(OLE32)]
        public static extern int GetRunningObjectTable(int reserved, out IRunningObjectTable prot);

        [DllImport(ADVAPI32, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern unsafe bool GetTokenInformation(void* TokenHandle, TOKEN_INFORMATION_CLASS TokenInformationClass, void* TokenInformation, uint TokenInformationLength, out uint ReturnLength);

        [DllImport(ADVAPI32, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern unsafe bool OpenProcessToken(void* ProcessHandle, uint DesiredAccess, void** TokenHandle);


        public static (string stdout, int code) Bash(string command) => DoPlatformDependent(
            delegate
            {
                static string escape(char c) => "^[]|()<>&'\"=$".Contains(c) ? "^" + c : c.ToString();

                return Run("cmd.exe", $"/c \"{string.Concat(command.Select(escape))}\"");
            },
            () => Run("/bin/bash", $"-c \"{command.Replace("\"", "\\\"")}\"")
        );

        private static (string stdout, int code) Run(string filename, string arguments)
        {
            using Process process = new Process()
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = filename,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = false,
                }
            };

            process.Start();

            string result = process.StandardOutput.ReadToEnd();

            process.WaitForExit();

            return (result, process.ExitCode);
        }

        public static T DoPlatformDependent<T>(T on_windows, T on_unix) => DoPlatformDependent(on_windows, on_unix, on_unix);

        public static T DoPlatformDependent<T>(T on_windows, T on_linux, T on_macos) => OperatingSystem switch
        {
            OperatingSystem.Windows => on_windows,
            OperatingSystem.Unix => on_linux,
            OperatingSystem.MacOS => on_macos,
        };

        public static void DoPlatformDependent(Action on_windows, Action on_unix) => DoPlatformDependent(on_windows, on_unix, on_unix);

        public static void DoPlatformDependent(Action on_windows, Action on_linux, Action on_macos) =>
            DoPlatformDependent<__empty>(() => { on_windows(); return default; }, () => { on_linux(); return default; }, () => { on_macos(); return default; });

        public static T DoPlatformDependent<T>(Func<T> on_windows, Func<T> on_unix) => DoPlatformDependent(on_windows, on_unix, on_unix);

        public static T DoPlatformDependent<T>(Func<T> on_windows, Func<T> on_linux, Func<T> on_macos) => OperatingSystem switch
        {
            OperatingSystem.Windows => on_windows(),
            OperatingSystem.Unix => on_linux(),
            OperatingSystem.MacOS => on_macos(),
        };

        public static async Task DoPlatformDependent(Task on_windows, Task on_unix) => await DoPlatformDependent(on_windows, on_unix, on_unix);

        public static async Task DoPlatformDependent(Task on_windows, Task on_linux, Task on_macos) => await (OperatingSystem switch
        {
            OperatingSystem.Windows => on_windows,
            OperatingSystem.Unix => on_linux,
            OperatingSystem.MacOS => on_macos,
        });

        public static async Task<T> DoPlatformDependent<T>(Task<T> on_windows, Task<T> on_unix) => await DoPlatformDependent(on_windows, on_unix, on_unix);

        public static async Task<T> DoPlatformDependent<T>(Task<T> on_windows, Task<T> on_linux, Task<T> on_macos) => await (OperatingSystem switch
        {
            OperatingSystem.Windows => on_windows,
            OperatingSystem.Unix => on_linux,
            OperatingSystem.MacOS => on_macos,
        });
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public unsafe struct SHFILEOPSTRUCT
    {
        public void* hwnd;
        public FileFuncFlags wFunc;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string pFrom;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string? pTo;
        public FILEOP_FLAGS fFlags;
        [MarshalAs(UnmanagedType.Bool)]
        public bool fAnyOperationsAborted;
        public void* hNameMappings;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string lpszProgressTitle;
    }

    [Flags]
    public enum FILEOP_FLAGS
        : ushort
    {
        FOF_MULTIDESTFILES = 0x1,
        FOF_CONFIRMMOUSE = 0x2,
        /// <summary>
        /// Don't create progress/report
        /// </summary>
        FOF_SILENT = 0x4,
        FOF_RENAMEONCOLLISION = 0x8,
        /// <summary>
        /// Don't prompt the user.
        /// </summary>
        FOF_NOCONFIRMATION = 0x10,
        /// <summary>
        /// Fill in SHFILEOPSTRUCT.hNameMappings.
        /// Must be freed using SHFreeNameMappings
        /// </summary>
        FOF_WANTMAPPINGHANDLE = 0x20,
        FOF_ALLOWUNDO = 0x40,
        /// <summary>
        /// On *.*, do only files
        /// </summary>
        FOF_FILESONLY = 0x80,
        /// <summary>
        /// Don't show names of files
        /// </summary>
        FOF_SIMPLEPROGRESS = 0x100,
        /// <summary>
        /// Don't confirm making any needed dirs
        /// </summary>
        FOF_NOCONFIRMMKDIR = 0x200,
        /// <summary>
        /// Don't put up error UI
        /// </summary>
        FOF_NOERRORUI = 0x400,
        /// <summary>
        /// Dont copy NT file Security Attributes
        /// </summary>
        FOF_NOCOPYSECURITYATTRIBS = 0x800,
        /// <summary>
        /// Don't recurse into directories.
        /// </summary>
        FOF_NORECURSION = 0x1000,
        /// <summary>
        /// Don't operate on connected elements.
        /// </summary>
        FOF_NO_CONNECTED_ELEMENTS = 0x2000,
        /// <summary>
        /// During delete operation,
        /// warn if nuking instead of recycling (partially overrides FOF_NOCONFIRMATION)
        /// </summary>
        FOF_WANTNUKEWARNING = 0x4000,
        /// <summary>
        /// Treat reparse points as objects, not containers
        /// </summary>
        FOF_NORECURSEREPARSE = 0x8000
    }

    [Flags]
    public enum FileFuncFlags
        : uint
    {
        FO_MOVE = 0x1,
        FO_COPY = 0x2,
        FO_DELETE = 0x3,
        FO_RENAME = 0x4
    }

    [Flags]
    public enum RecycleFlags
        : uint
    {
        SHERB_NOCONFIRMATION = 0x00000001,
        SHERB_NOPROGRESSUI = 0x00000002,
        SHERB_NOSOUND = 0x00000004
    }

    public enum OperatingSystem
    {
        Windows,
        Unix,
        MacOS
    }

    public enum TOKEN_INFORMATION_CLASS
    {
        TokenUser,
        TokenGroups,
        TokenPrivileges,
        TokenOwner,
        TokenPrimaryGroup,
        TokenDefaultDacl,
        TokenSource,
        TokenType,
        TokenImpersonationLevel,
        TokenStatistics,
        TokenRestrictedSids,
        TokenSessionId,
        TokenGroupsAndPrivileges,
        TokenSessionReference,
        TokenSandBoxInert,
        TokenAuditPolicy,
        TokenOrigin,
        TokenElevationType,
        TokenLinkedToken,
        TokenElevation,
        TokenHasRestrictions,
        TokenAccessInformation,
        TokenVirtualizationAllowed,
        TokenVirtualizationEnabled,
        TokenIntegrityLevel,
        TokenUIAccess,
        TokenMandatoryPolicy,
        TokenLogonSid,
        TokenIsAppContainer,
        TokenCapabilities,
        TokenAppContainerSid,
        TokenAppContainerNumber,
        TokenUserClaimAttributes,
        TokenDeviceClaimAttributes,
        TokenRestrictedUserClaimAttributes,
        TokenRestrictedDeviceClaimAttributes,
        TokenDeviceGroups,
        TokenRestrictedDeviceGroups,
        TokenSecurityAttributes,
        TokenIsRestricted,
        TokenProcessTrustLevel,
        TokenPrivateNameSpace,
        TokenSingletonAttributes,
        TokenBnoIsolation,
        TokenChildProcessFlags,
        TokenIsLessPrivilegedAppContainer,
        TokenIsSandboxed,
        TokenOriginatingProcessTrustLevel,
        MaxTokenInfoClass
    }
}
