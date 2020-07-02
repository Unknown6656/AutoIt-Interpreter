using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Unknown6656.AutoIt3.Runtime
{
    public static class NativeInterop
    {
        public static OperatingSystem OperatingSystem { get; } = Environment.OSVersion.Platform switch
        {
            PlatformID.MacOSX => OperatingSystem.MacOS,
            PlatformID.Unix => OperatingSystem.Unix,
            _ => OperatingSystem.Windows
        };



        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        internal static extern ushort GetUserDefaultUILanguage();


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

    public enum OperatingSystem
    {
        Windows,
        Unix,
        MacOS
    }
}
