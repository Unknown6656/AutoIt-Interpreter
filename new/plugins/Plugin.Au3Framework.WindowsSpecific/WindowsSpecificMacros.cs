using System.Runtime.InteropServices;

using Unknown6656.AutoIt3.Extensibility;
using Unknown6656.AutoIt3.Runtime;

[assembly: AutoIt3Plugin]


namespace Unknown6656.AutoIt3.Extensibility.Plugins.Au3Framework
{
    public sealed class WindowsSpecificMacros
        : AbstractKnownMacroProvider
    {
        public WindowsSpecificMacros(Interpreter interpreter)
            : base(interpreter)
        {
            RegisterMacro("DESKTOPHEIGHT", _ => DEVMODE.Current.dmPelsHeight);
            RegisterMacro("DESKTOPWIDTH", _ => DEVMODE.Current.dmPelsWidth);
            RegisterMacro("DESKTOPREFRESH", _ => DEVMODE.Current.dmDisplayFrequency);
            RegisterMacro("DesktopDepth", _ => DEVMODE.Current.dmBitsPerPel);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct DEVMODE
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 0x20)]
        public string dmDeviceName;
        public short dmSpecVersion;
        public short dmDriverVersion;
        public short dmSize;
        public short dmDriverExtra;
        public int dmFields;
        public int dmPositionX;
        public int dmPositionY;
        public int dmDisplayOrientation;
        public int dmDisplayFixedOutput;
        public short dmColor;
        public short dmDuplex;
        public short dmYResolution;
        public short dmTTOption;
        public short dmCollate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 0x20)]
        public string dmFormName;
        public short dmLogPixels;
        public int dmBitsPerPel;
        public int dmPelsWidth;
        public int dmPelsHeight;
        public int dmDisplayFlags;
        public int dmDisplayFrequency;
        public int dmICMMethod;
        public int dmICMIntent;
        public int dmMediaType;
        public int dmDitherType;
        public int dmReserved1;
        public int dmReserved2;
        public int dmPanningWidth;
        public int dmPanningHeight;


        public static DEVMODE Current
        {
            get
            {
                [DllImport("user32.dll")]
                static extern bool EnumDisplaySettings(string? deviceName, int modeNum, ref DEVMODE devMode);

                DEVMODE mode = default;
                mode.dmSize = (short)Marshal.SizeOf(mode);

                EnumDisplaySettings(null, -1, ref mode);

                return mode;
            }
        }
    }
}
