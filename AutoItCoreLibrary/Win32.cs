using System.Runtime.InteropServices;
using System;

namespace AutoItCoreLibrary
{
    public static unsafe class Win32
    {
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
    }

    public enum OS
    {
        Windows,
        Linux,
        MacOS
    }
}
