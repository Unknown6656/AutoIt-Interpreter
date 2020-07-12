using System.Runtime.InteropServices;
using System;

namespace Unknown6656.AutoIt3.COM.Server
{
    [ComImport, Guid("0000010c-0000-0000-c000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IPersist
    {
        [PreserveSig]
        void GetClassID(out Guid pClassID);
    }

    internal static unsafe class NativeInterop
    {
        [DllImport("ole32.dll")]
        public static extern int ProgIDFromCLSID([In] Guid* clsid, [MarshalAs(UnmanagedType.LPWStr)] out string? lplpszProgID);
    }
}
