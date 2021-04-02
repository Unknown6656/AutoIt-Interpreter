using System.Runtime.InteropServices.ComTypes;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System;

namespace Unknown6656.AutoIt3.Runtime.Native
{
    /// <summary>
    /// A static module containing functions and constants for native interop.
    /// </summary>
    public static class NativeInterop
    {
        #region CONSTANTS

        public const uint TOKEN_READ = 0x00020008;
        public const uint TOKEN_ADJUST_PRIVILEGES = 0x00000020;
        public const uint TOKEN_QUERY = 0x00000008;

        public const int MAX_PATH = 255;
        public const string LIBC6_SO = "libc.so"; // libc.so.6
        public const string LIBDL_SO = "libdl.so"; // libdl.so.2

        public const string LIBDL_DYLIB = "libdl.dylib";

        public const string KERNEL32_DLL = "kernel32.dll";
        public const string SHELL32_DLL = "shell32.dll";
        public const string OLE32_DLL = "ole32.dll";
        public const string USER32_DLL = "user32.dll";
        public const string COREDLL_DLL = "coredll.dll";
        public const string NTDLL_DLL = "ntdll.dll";
        public const string ADVAPI32_DLL = "advapi32.dll";
        public const string POWRPROF_DLL = "powrprof.dll";

        #endregion

        public static OS OperatingSystem { get; } = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? OS.Windows
            : RuntimeInformation.IsOSPlatform(OSPlatform.FreeBSD) ? OS.UnixLike
            : RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? OS.Linux
            : RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? OS.MacOS
            : Environment.OSVersion.Platform switch
            {
                PlatformID.MacOSX => OS.MacOS,
                PlatformID.Unix => OS.UnixLike,
                _ => OS.Windows
            };

        #region LIBC.SO

        [DllImport(LIBC6_SO, EntryPoint = "geteuid")]
        public static unsafe extern uint Linux__geteuid();

        [DllImport(LIBC6_SO, EntryPoint = "ioctl")]
        public static unsafe extern int Linux__ioctl(int fd, ulong req, __arglist);

        [DllImport(LIBC6_SO, EntryPoint = "reboot")]
        public static unsafe extern int Linux__reboot(uint magic, uint magic2, uint cmd, void* arg);

        [DllImport(LIBC6_SO, EntryPoint = "open", CharSet = CharSet.Ansi)]
        public static unsafe extern int Linux__open([MarshalAs(UnmanagedType.LPStr)] string path, int flags);

        #endregion
        #region LIBDL.SO

        [DllImport(LIBDL_SO, EntryPoint = "dlopen", CharSet = CharSet.Ansi)]
        public static extern nint Linux__dlopen([MarshalAs(UnmanagedType.LPStr)] string filename, int flags = 0x0101);

        [DllImport(LIBDL_SO, EntryPoint = "dlsym", CharSet = CharSet.Ansi)]
        public static extern nint Linux__dlsym(nint handle, [MarshalAs(UnmanagedType.LPStr)] string funcname);

        [DllImport(LIBDL_SO, EntryPoint = "dlclose")]
        public static extern nint Linux__dlclose(nint handle);

        #endregion
        #region LIBDL.DYLIB

        [DllImport(LIBDL_DYLIB, EntryPoint = "dlopen", CharSet = CharSet.Ansi)]
        public static extern nint MacOS__dlopen([MarshalAs(UnmanagedType.LPStr)] string filename, int flags = 0x0101);

        [DllImport(LIBDL_DYLIB, EntryPoint = "dlsym", CharSet = CharSet.Ansi)]
        public static extern nint MacOS__dlsym(nint handle, [MarshalAs(UnmanagedType.LPStr)] string funcname);

        [DllImport(LIBDL_DYLIB, EntryPoint = "dlclose")]
        public static extern int MacOS__dlclose(nint handle);

        #endregion
        #region USER32.DLL

        [DllImport(USER32_DLL, SetLastError = true)]
        public static unsafe extern bool SetForegroundWindow(nint hWnd);

        [DllImport(USER32_DLL, CharSet = CharSet.Auto, SetLastError = true)]
        public static unsafe extern void* SetFocus(void* hWnd);

        [DllImport(USER32_DLL, SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern int MessageBox(int hWnd, string text, string title, uint type);

        [DllImport(USER32_DLL, SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern nint CreateWindowEx(int dwExStyle, string lpClassName, string lpWindowName, int dwStyle, int x, int y, int nWidth, int nHeight, nint hWndParent, nint hMenu, nint hInstance, nint lpParam);

        [DllImport(USER32_DLL, SetLastError = true, CharSet = CharSet.Auto)]
        public static extern nint SetWindowLongPtr(nint hWnd, int nIndex, nint dwNewLong);

        [DllImport(USER32_DLL, SetLastError = true)]
        public static extern nint SetParent(nint hWnd, nint hWndNewParent);

        [DllImport(USER32_DLL, CharSet = CharSet.Auto)]
        public static extern nint SendMessage(nint hWnd, int Msg, nint wParam, nint lParam);

        [DllImport(USER32_DLL, CharSet = CharSet.Auto)]
        public static extern nint SendMessage(nint hWnd, int Msg, nint wParam, [MarshalAs(UnmanagedType.LPWStr)] string lParam);

        #endregion
        #region SHELL32.DLL

        [DllImport(SHELL32_DLL, CharSet = CharSet.Unicode)]
        public static extern int SHFileOperation(ref SHFILEOPSTRUCT lpFileOp);

        [DllImport(SHELL32_DLL, CharSet = CharSet.Unicode)]
        public static unsafe extern uint SHEmptyRecycleBin(void* hwnd, string? pszRootPath, RecycleFlags dwFlags);

        #endregion
        #region KERNEL32.DLL

        [DllImport(KERNEL32_DLL, CharSet = CharSet.Auto)]
        public static extern ushort GetUserDefaultUILanguage();

        [DllImport(KERNEL32_DLL, SetLastError = true)]
        public static extern unsafe void* GetCurrentProcess();

        [DllImport(KERNEL32_DLL)]
        public static extern int GetLastError();

        [DllImport(KERNEL32_DLL)]
        public static extern void SetLastError(int error);

        [DllImport(KERNEL32_DLL)]
        public static extern unsafe nint LocalFree(void* hMem);

        [DllImport(KERNEL32_DLL, SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern nint LoadLibrary(string lpLibFileName);

        [DllImport(KERNEL32_DLL, SetLastError = true)]
        public static extern nint GetProcAddress(nint hLibModule, string lpProcName);

        [DllImport(KERNEL32_DLL, SetLastError = true)]
        public static extern bool FreeLibrary(nint hLibModule);

        [DllImport(KERNEL32_DLL)]
        public static extern ulong VerSetConditionMask(ulong ConditionMask, int TypeMask, byte Condition);

        [DllImport(KERNEL32_DLL)]
        public static extern unsafe bool VerifyVersionInfo(OSVERSIONINFOEXW* lpVersionInformation, int dwTypeMask, ulong dwlConditionMask);

        [DllImport(KERNEL32_DLL, CharSet = CharSet.Auto)]
        public static extern bool GetVolumeInformation(string letter, StringBuilder name, int nameSize, out uint serialNumber, out uint maximumComponentLength, out uint flags, StringBuilder systemName, int systemNameSize);

        #endregion
        #region NTDLL.DLL

        [DllImport(NTDLL_DLL)]
        public static extern int RtlAdjustPrivilege(int Privilege, bool bEnablePrivilege, bool IsThreadPrivilege, out bool _);

        [DllImport(NTDLL_DLL)]
        public static unsafe extern int NtRaiseHardError(uint ErrorStatus, uint NumberOfParameters, uint UnicodeStringParameterMask, void* Parameters, uint ValidResponseOption, out uint _);

        #endregion
        #region COREDLL.DLL

        [DllImport(COREDLL_DLL, SetLastError = true)]
        public static unsafe extern bool DeviceIoControl(void* hDevice, int dwIoControlCode, byte* lpInBuffer, int nInBufferSize, byte* lpOutBuffer, int nOutBufferSize, int* lpBytesReturned, void* lpOverlapped);

        [DllImport(COREDLL_DLL, CharSet = CharSet.Unicode, SetLastError = true)]
        public static unsafe extern void* CreateFile(string lpFileName, uint dwDesiredAccess, uint dwShareMode, void* lpSecurityAttributes, uint dwCreationDisposition, uint dwFlagsAndAttributes, void* hTemplateFile);

        #endregion
        #region USER32.DLL

        [DllImport(USER32_DLL)]
        public static extern bool BlockInput(bool fBlockIt);

        [DllImport(USER32_DLL)]
        public static extern int ShowWindow(int hwnd, int nCmdShow);

        #endregion
        #region OLE32.DLL

        [DllImport(OLE32_DLL)]
        public static extern int CreateBindCtx(int reserved, out IBindCtx ppbc);

        [DllImport(OLE32_DLL)]
        public static extern int GetRunningObjectTable(int reserved, out IRunningObjectTable prot);

        #endregion
        #region ADVAPI32.DLL

        [DllImport(ADVAPI32_DLL, CharSet = CharSet.Unicode, SetLastError = true)]
        public static unsafe extern int RegDeleteKeyEx(void* hKey, string lpSubKey, RegSAM samDesired, void* lpReserved);

        [DllImport(ADVAPI32_DLL, CharSet = CharSet.Unicode, SetLastError = true)]
        public static unsafe extern int RegDeleteKeyValue(void* hKey, string lpSubKey, string lpValueName);

        [DllImport(ADVAPI32_DLL, CharSet = CharSet.Unicode, SetLastError = false)]
        public static unsafe extern int RegCreateKeyEx(void* hKey, string lpSubKey, void* lpReserved, void* lpClass, RegOption dwOptions, RegSAM samDesired, SECURITY_ATTRIBUTES* lpSecurityAttributes, out void* phkResult, out RegResult lpdwDisposition);

        [DllImport(ADVAPI32_DLL, CharSet = CharSet.Unicode, SetLastError = true)]
        public static unsafe extern int RegEnumKeyEx(void* hkey, int index, StringBuilder lpName, int* lpcbName, void* lpReserved, void* lpClass, void* lpcbClass, out long lpftLastWriteTime);

        [DllImport(ADVAPI32_DLL, CharSet = CharSet.Unicode, SetLastError = true)]
        public static unsafe extern int RegEnumValue( void* hKey, int dwIndex, StringBuilder lpValueName, int* lpcValueName, void* lpReserved, out RegKeyType lpType, void* lpData, out int lpcbData);

        [DllImport(ADVAPI32_DLL, CharSet = CharSet.Unicode, SetLastError = true)]
        public static unsafe extern int RegOpenKeyEx(void* hKey, string subKey, int ulOptions, RegSAM samDesired, out void* hkResult);

        // [DllImport(ADVAPI32, CharSet = CharSet.Unicode, SetLastError = true)]
        // public static unsafe extern int RegQueryValueEx(void* hKey, string lpValueName, void* lpReserved, out RegKeyType lpType, out void* lpData, out uint lpcbData);

        [DllImport(ADVAPI32_DLL, CharSet = CharSet.Unicode, SetLastError = true)]
        public static unsafe extern int RegSetValueEx(void* hKey, string lpValueName, void* lpReserved, RegKeyType dwType, void* lpData, int cbData);

        [DllImport(ADVAPI32_DLL, CharSet = CharSet.Unicode, SetLastError = true)]
        public static unsafe extern int RegGetValue(void* hKey, string lpSubKey, string lpValue, int dwFlags, out RegKeyType pdwType, void* pvData, out int pcbData);

        [DllImport(ADVAPI32_DLL, SetLastError = true)]
        public static unsafe extern int RegCloseKey(void* hKey);

        [DllImport(ADVAPI32_DLL, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern unsafe bool GetTokenInformation(void* TokenHandle, TOKEN_INFORMATION_CLASS TokenInformationClass, void* TokenInformation, uint TokenInformationLength, out uint ReturnLength);

        [DllImport(ADVAPI32_DLL, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern unsafe bool OpenProcessToken(void* ProcessHandle, uint DesiredAccess, void** TokenHandle);

        [DllImport(ADVAPI32_DLL, CharSet = CharSet.Auto, SetLastError = true)]
        public static extern unsafe int InitiateShutdown([MarshalAs(UnmanagedType.LPWStr)] string? lpMachineName, [MarshalAs(UnmanagedType.LPWStr)] string? lpMessage, uint dwTimeout, uint flags, uint dwReason);

        [DllImport(ADVAPI32_DLL, CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool LookupPrivilegeValue([MarshalAs(UnmanagedType.LPWStr)] string? lpSystemName, [MarshalAs(UnmanagedType.LPWStr)] string? lpName, ref (uint low, int high) lpLuid);

        [DllImport(ADVAPI32_DLL, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern unsafe bool AdjustTokenPrivileges(void* TokenHandle, [MarshalAs(UnmanagedType.Bool)] bool DisableAllPrivileges, ref TOKEN_PRIVILEGES NewState, int BufferLengthInBytes, void* PreviousState, int* ReturnLengthInBytes);

        #endregion
        #region POWRPROF.DLL

        [DllImport(POWRPROF_DLL, CharSet = CharSet.Auto, ExactSpelling = true)]
        public static extern bool SetSuspendState(bool hiberate, bool forceCritical, bool disableWakeEvent);

        #endregion

        public static void AddFolderToEnvPath(string dir)
        {
            char separator = NativeInterop.DoPlatformDependent(';', ':');
            List<string> path = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Process)?
                                           .Split(separator, StringSplitOptions.RemoveEmptyEntries)
                                           .ToList() ?? new();

            path.Add(dir);

            Environment.SetEnvironmentVariable("PATH", string.Join(separator, path.Distinct()), EnvironmentVariableTarget.Process);
        }

        public static (string stdout, int code) Exec(string command, bool use_shellexec = false) => DoPlatformDependent(
            () => InternalRun("cmd.exe", new[] { "/c", command }, use_shellexec),
            () => InternalRun("/bin/bash", new[] { "-c", command }, use_shellexec)
        );

        private static (string stdout, int code) InternalRun(string filename, string[] arguments, bool use_shexec)
        {
            static string escape(char c) => "^[]|()<>&'\"=$".Contains(c, StringComparison.InvariantCulture) ? "^" + c : c.ToString();
            using Process process = new Process()
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = filename,
                    RedirectStandardOutput = true,
                    UseShellExecute = use_shexec,
                    CreateNoWindow = false,
                }
            };

            foreach (string arg in arguments)
                process.StartInfo.ArgumentList.Add(arg); // todo : escape ?

            process.Start();

            string result = process.StandardOutput.ReadToEnd();

            process.WaitForExit();

            return (result, process.ExitCode);
        }

        public static unsafe bool IsWindowsServer()
        {
            OSVERSIONINFOEXW osvi = new OSVERSIONINFOEXW
            {
                dwOSVersionInfoSize = sizeof(OSVERSIONINFOEXW),
                wProductType = 1
            };
            ulong mask = VerSetConditionMask(0, 0x0080, 1);

            return !VerifyVersionInfo(&osvi, 0x0080, mask);
        }

        public static T DoPlatformDependent<T>(T on_windows, T on_unix) => DoPlatformDependent(on_windows, on_unix, on_unix);

        public static T DoPlatformDependent<T>(T on_windows, T on_linux, T on_macos) => OperatingSystem switch
        {
            OS.Windows => on_windows,
            OS.Linux => on_linux,
            OS.MacOS => on_macos,
            OS.UnixLike => on_linux,
        };

        public static void DoPlatformDependent(Action action, params OS[] os)
        {
            if (os.Contains(OperatingSystem))
                action();
        }

        public static void DoPlatformDependent(Action on_windows, Action on_unix) => DoPlatformDependent(on_windows, on_unix, on_unix);

        public static void DoPlatformDependent(Action on_windows, Action on_linux, Action on_macos) =>
            DoPlatformDependent<__empty>(() => { on_windows(); return default; }, () => { on_linux(); return default; }, () => { on_macos(); return default; });

        public static T DoPlatformDependent<T>(Func<T> on_windows, Func<T> on_unix) => DoPlatformDependent(on_windows, on_unix, on_unix);

        public static T DoPlatformDependent<T>(Func<T> on_windows, Func<T> on_linux, Func<T> on_macos) => OperatingSystem switch
        {
            OS.Windows => on_windows(),
            OS.Linux => on_linux(),
            OS.MacOS => on_macos(),
            OS.UnixLike => on_linux(),
        };

        public static async Task DoPlatformDependent(Task on_windows, Task on_unix) => await DoPlatformDependent(on_windows, on_unix, on_unix);

        public static async Task DoPlatformDependent(Task on_windows, Task on_linux, Task on_macos) => await (OperatingSystem switch
        {
            OS.Windows => on_windows,
            OS.Linux => on_linux,
            OS.MacOS => on_macos,
            OS.UnixLike => on_linux,
        });

        public static async Task<T> DoPlatformDependent<T>(Task<T> on_windows, Task<T> on_unix) => await DoPlatformDependent(on_windows, on_unix, on_unix);

        public static async Task<T> DoPlatformDependent<T>(Task<T> on_windows, Task<T> on_linux, Task<T> on_macos) => await (OperatingSystem switch
        {
            OS.Windows => on_windows,
            OS.Linux => on_linux,
            OS.MacOS => on_macos,
            OS.UnixLike => on_linux,
        });
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct OSVERSIONINFOEXW
    {
        public int dwOSVersionInfoSize;
        public int dwMajorVersion;
        public int dwMinorVersion;
        public int dwBuildNumber;
        public int dwPlatformId;
        public fixed char szCSDVersion[128];
        public short wServicePackMajor;
        public short wServicePackMinor;
        public short wSuiteMask;
        public byte wProductType;
        public byte wReserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct TOKEN_PRIVILEGES
    {
        public int PrivilegeCount;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1)]
        public LUID_AND_ATTRIBUTES[] Privileges;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct LUID_AND_ATTRIBUTES
    {
        public (uint low, int high) Luid;
        public uint Attributes;
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

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct SECURITY_ATTRIBUTES
    {
        public int nLength;
        public void* lpSecurityDescriptor;
        public bool bInheritHandle;
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

    [Flags]
    public enum OS
         : byte
    {
        Unknown = 0,
        Windows = 1,
        Linux = 2,
        MacOS = 4,
        UnixLike = Linux | MacOS,
        Any = Windows | UnixLike,
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

    public enum RegKeyType
    {
        REG_NONE = -1,
        REG_UNKNOWN = 0,
        REG_SZ = 1,
        REG_EXPAND_SZ = 2,
        REG_BINARY = 3,
        REG_DWORD = 4,
        REG_DWORD_LITTLE_ENDIAN = 4,
        REG_DWORD_BIG_ENDIAN = 5,
        REG_QWORD = 11,
        REG_LINK = 6,
        REG_MULTI_SZ = 7,
    }

    public enum RegPredefinedkeys
        : uint
    {
        HKEY_CLASSES_ROOT = 0x80000000,
        HKEY_CURRENT_USER = 0x80000001,
        HKEY_LOCAL_MACHINE = 0x80000002,
        HKEY_USERS = 0x80000003,
        HKEY_PERFORMANCE_DATA = 0x80000004,
        HKEY_CURRENT_CONFIG = 0x80000005,
        HKEY_DYN_DATA = 0x80000006,
    }

    [Flags]
    public enum RegOption
    {
        NonVolatile = 0x0,
        Volatile = 0x1,
        CreateLink = 0x2,
        BackupRestore = 0x4,
        OpenLink = 0x8
    }

    [Flags]
    public enum RegSAM
    {
        QueryValue = 0x0001,
        SetValue = 0x0002,
        CreateSubKey = 0x0004,
        EnumerateSubKeys = 0x0008,
        Notify = 0x0010,
        CreateLink = 0x0020,
        WOW64_32Key = 0x0200,
        WOW64_64Key = 0x0100,
        WOW64_Res = 0x0300,
        Read = 0x00020019,
        Write = 0x00020006,
        Execute = 0x00020019,
        AllAccess = 0x000f003f
    }

    public enum RegResult
    {
        CreatedNewKey = 0x00000001,
        OpenedExistingKey = 0x00000002
    }
}
