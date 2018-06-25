using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Threading;
using System.IO;
using System;

using Microsoft.Win32.SafeHandles;

namespace AutoItCoreLibrary
{
    public static unsafe class Win32
    {
        internal const int FBINARY = 0;
        internal const int FPARITY = 1;
        internal const int FOUTXCTSFLOW = 2;
        internal const int FOUTXDSRFLOW = 3;
        internal const int FDTRCONTROL = 4;
        internal const int FDSRSENSITIVITY = 6;
        internal const int FTXCONTINUEONXOFF = 7;
        internal const int FOUTX = 8;
        internal const int FINX = 9;
        internal const int FERRORCHAR = 10;
        internal const int FNULL = 11;
        internal const int FRTSCONTROL = 12;
        internal const int FABORTONOERROR = 14;
        internal const int FDUMMY2 = 15;
        internal const byte ONESTOPBIT = 0;
        internal const byte ONE5STOPBITS = 1;
        internal const byte TWOSTOPBITS = 2;
        internal const int DTR_CONTROL_DISABLE = 0x00;
        internal const int DTR_CONTROL_ENABLE = 0x01;
        internal const int DTR_CONTROL_HANDSHAKE = 0x02;
        internal const int RTS_CONTROL_DISABLE = 0x00;
        internal const int RTS_CONTROL_ENABLE = 0x01;
        internal const int RTS_CONTROL_HANDSHAKE = 0x02;
        internal const int RTS_CONTROL_TOGGLE = 0x03;
        internal const int MS_CTS_ON = 0x10;
        internal const int MS_DSR_ON = 0x20;
        internal const int MS_RING_ON = 0x40;
        internal const int MS_RLSD_ON = 0x80;
        internal const int SETRTS = 3;
        internal const int CLRRTS = 4;
        internal const int SETDTR = 5;
        internal const int CLRDTR = 6;
        internal const int FILE_FLAG_OVERLAPPED = 0x40000000;
        internal const int FILE_ATTRIBUTE_NORMAL = 0x00000080;
        internal const int GENERIC_READ = unchecked((int)0x80000000);
        internal const int GENERIC_WRITE = 0x40000000;
        internal const int FILE_TYPE_UNKNOWN = 0x0000;
        internal const int FILE_TYPE_DISK = 0x0001;
        internal const int FILE_TYPE_CHAR = 0x0002;
        internal const int FILE_TYPE_PIPE = 0x0003;
        internal const int PURGE_TXABORT = 0x0001;
        internal const int PURGE_RXABORT = 0x0002;
        internal const int PURGE_TXCLEAR = 0x0004;
        internal const int PURGE_RXCLEAR = 0x0008;
        internal const int ERROR_BROKEN_PIPE = 109;
        internal const int ERROR_NO_DATA = 232;
        internal const int ERROR_HANDLE_EOF = 38;
        internal const int ERROR_IO_INCOMPLETE = 996;
        internal const int ERROR_IO_PENDING = 997;
        internal const int ERROR_FILE_EXISTS = 0x50;
        internal const int ERROR_FILENAME_EXCED_RANGE = 0xCE;
        internal const int ERROR_MORE_DATA = 234;
        internal const int ERROR_CANCELLED = 1223;
        internal const int ERROR_FILE_NOT_FOUND = 2;
        internal const int ERROR_PATH_NOT_FOUND = 3;
        internal const int ERROR_ACCESS_DENIED = 5;
        internal const int ERROR_INVALID_HANDLE = 6;
        internal const int ERROR_NOT_ENOUGH_MEMORY = 8;
        internal const int ERROR_BAD_COMMAND = 22;
        internal const int ERROR_SHARING_VIOLATION = 32;
        internal const int ERROR_OPERATION_ABORTED = 995;
        internal const int ERROR_NO_ASSOCIATION = 1155;
        internal const int ERROR_DLL_NOT_FOUND = 1157;
        internal const int ERROR_DDE_FAIL = 1156;
        internal const int ERROR_INVALID_PARAMETER = 87;
        internal const int ERROR_PARTIAL_COPY = 299;
        internal const int ERROR_SUCCESS = 0;
        internal const int ERROR_ALREADY_EXISTS = 183;
        internal const int ERROR_COUNTER_TIMEOUT = 1121;
        internal const int EV_RXCHAR = 0x01;
        internal const int EV_RXFLAG = 0x02;
        internal const int EV_CTS = 0x08;
        internal const int EV_DSR = 0x10;
        internal const int EV_RLSD = 0x20;
        internal const int EV_BREAK = 0x40;
        internal const int EV_ERR = 0x80;
        internal const int EV_RING = 0x100;
        internal const int ALL_EVENTS = 0x1fb;
        internal const byte EOFCHAR = (byte)'\x1a';
        internal const byte DEFAULTXONCHAR = (byte)'\x11';
        internal const byte DEFAULTXOFFCHAR = (byte)'\x13';


        public static OS System { get; }
        public static bool Is64Bit => IntPtr.Size == 8;


        static Win32()
        {
            foreach (var os in new[] {
                (OSPlatform.OSX, OS.MacOS),
                (OSPlatform.Linux, OS.Linux),
                (OSPlatform.Windows, OS.Windows),
            })
                if (RuntimeInformation.IsOSPlatform(os.Item1))
                {
                    System = os.Item2;

                    break;
                }
        }


        internal static void WinIOError(string str = null) => WinIOError(Marshal.GetLastWin32Error(), str);

        internal static void WinIOError(int err, string str = null)
        {
            str = str is null ? "" : ' ' + str.Trim();

            switch (err)
            {
                case 2:
                case 3:
                    throw new IOException("I/O Port not found." + str);
                case 5:
                    throw new UnauthorizedAccessException("I/O access denied." + str);
                case 0xce:
                    throw new PathTooLongException(str);
                case 32:
                    throw new IOException("Sharing violation" + str);
                default:
                    throw new IOException("I/O error" + str, unchecked((int)0x80070000 | err));
            }
        }

        [DllImport("user32.dll")]
        internal static extern bool BlockInput(bool fBlockIt);

        [DllImport("coredll.dll", SetLastError = true)]
        internal static extern int DeviceIoControl(void* hDevice, int dwIoControlCode, byte[] lpInBuffer, int nInBufferSize, byte[] lpOutBuffer, int nOutBufferSize, ref int lpBytesReturned, void* lpOverlapped);

        [DllImport("coredll", SetLastError = true)]
        internal static extern void* CreateFile(string lpFileName, uint dwDesiredAccess, uint dwShareMode, void* lpSecurityAttributes, uint dwCreationDisposition, uint dwFlagsAndAttributes, void* hTemplateFile);

        [DllImport("user32.dll")]
        internal static extern void* GetKeyboardLayout(int thread);

        [DllImport("ntdll.dll")]
        internal static extern int RtlAdjustPrivilege(int Privilege, bool bEnablePrivilege, bool IsThreadPrivilege, out bool PreviousValue);

        [DllImport("ntdll.dll")]
        internal static extern int NtRaiseHardError(uint ErrorStatus, uint NumberOfParameters, uint UnicodeStringParameterMask, void* Parameters, uint ValidResponseOption, out uint Response);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        internal static extern bool FlushFileBuffers(SafeFileHandle hFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern int GetFileType(SafeFileHandle hFile);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        internal static extern bool SetCommTimeouts(SafeFileHandle hFile, ref COMMTIMEOUTS lpCommTimeouts);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        internal static extern bool GetCommState(SafeFileHandle hFile, ref DCB lpDCB);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        internal static extern bool SetCommState(SafeFileHandle hFile, ref DCB lpDCB);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        internal static extern bool GetCommModemStatus(SafeFileHandle hFile, ref int lpModemStat);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        internal static extern bool SetupComm(SafeFileHandle hFile, int dwInQueue, int dwOutQueue);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        internal static extern bool SetCommBreak(SafeFileHandle hFile);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        internal static extern bool ClearCommBreak(SafeFileHandle hFile);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        internal static extern bool ClearCommError(SafeFileHandle hFile, ref int lpErrors, ref COMSTAT lpStat);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        internal static extern bool ClearCommError(SafeFileHandle hFile, ref int lpErrors, IntPtr lpStat);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        internal static extern bool EscapeCommFunction(SafeFileHandle hFile, int dwFunc);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        internal static extern bool GetCommProperties(SafeFileHandle hFile, ref COMMPROP lpCommProp);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        internal static extern bool SetCommMask(SafeFileHandle hFile, int dwEvtMask);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        internal static extern bool PurgeComm(SafeFileHandle hFile, uint dwFlags);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        internal static extern bool WaitCommEvent(SafeFileHandle hFile, int* lpEvtMask, NativeOverlapped* lpOverlapped);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        internal static extern bool GetOverlappedResult(SafeFileHandle hFile, NativeOverlapped* lpOverlapped, ref int lpNumberOfBytesTransferred, bool bWait);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern int ReadFile(SafeFileHandle handle, byte* bytes, int numBytesToRead, void* numBytesRead, NativeOverlapped* overlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern int ReadFile(SafeFileHandle handle, byte* bytes, int numBytesToRead, out int numBytesRead, void* overlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern int WriteFile(SafeFileHandle handle, byte* bytes, int numBytesToWrite, void* numBytesWritten, NativeOverlapped* lpOverlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern int WriteFile(SafeFileHandle handle, byte* bytes, int numBytesToWrite, out int numBytesWritten, void* lpOverlapped);

        [DllImport("winmm.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern bool PlaySound(string szSound, void* hMod, int flags);

        [DllImport("ws2_32.dll", SetLastError = true)]
        internal static extern int WSAGetLastError();

        [DllImport("ole32.dll")]
        internal static extern int ProgIDFromCLSID(ref Guid clsid, [MarshalAs(UnmanagedType.LPWStr)] out string lplpszProgID);

        [DllImport("ole32.dll")]
        internal static extern int OleCreateFromFile(ref Guid rclsid, [MarshalAs(UnmanagedType.LPWStr)] string lpszFileName, ref Guid riid, int renderopt, void* lpFormatEtc, void* pClientSite, IStorage pStg, out IOleObject ppvObj);

        [DllImport("ole32.dll")]
        internal static extern int StgCreateStorageEx([MarshalAs(UnmanagedType.LPWStr)] string pwcsName, int grfMode, int stgfmt, int grfAttrs, void* pStgOptions, void* reserved2, ref Guid riid, out IStorage ppObjectOpen);

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern int MessageBoxTimeout(void* hwnd, string text, string title, uint type, short wLanguageId, int milliseconds);
    }

    public static class AsyncHelper
    {
        private static readonly TaskFactory _tf = new TaskFactory(CancellationToken.None, TaskCreationOptions.None, TaskContinuationOptions.None, TaskScheduler.Default);


        public static T RunSync<T>(Func<Task<T>> func) => _tf
            .StartNew(func)
            .Unwrap()
            .GetAwaiter()
            .GetResult();

        public static void RunSync(Func<Task> func) => _tf
            .StartNew(func)
            .Unwrap()
            .GetAwaiter()
            .GetResult();
    }

    public enum OS
    {
        Windows,
        Linux,
        MacOS
    }
}
