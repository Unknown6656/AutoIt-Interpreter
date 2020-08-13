using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Sockets;
using System.Net;
using System.IO;
using System;

using Unknown6656.AutoIt3.Parser.ExpressionParser;
using Unknown6656.AutoIt3.Runtime.Native;
using Unknown6656.AutoIt3.Runtime;
using Unknown6656.Common;

namespace Unknown6656.AutoIt3.Extensibility.Plugins.Au3Framework
{
    using static MainProgram;
    using static AST;

    public sealed class FrameworkMacros
        : AbstractMacroProvider
    {
        internal const string MACRO_DISCARD = "DISCARD";
        private static readonly Regex REGEX_IPADDRESS = new Regex(@"ipaddress(?<num>\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly OperatingSystem _os = Environment.OSVersion;


        public FrameworkMacros(Interpreter interpreter)
            : base(interpreter)
        {
        }

        public unsafe override bool ProvideMacroValue(CallFrame frame, string name, out Variant? value)
        {
            SourceLocation location = frame.CurrentThread.CurrentLocation ?? Interpreter.MainThread?.CurrentLocation ?? SourceLocation.Unknown;

            value = name.ToUpperInvariant() switch
            {
                "APPDATACOMMONDIR" => Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "APPDATADIR" => Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "AUTOITEXE" => ASM_FILE.FullName,
                "AUTOITPID" => Process.GetCurrentProcess().Id,
                "AUTOITVERSION" => __module__.InterpreterVersion?.ToString() ?? "0.0.0.0",
                "AUTOITX64" => sizeof(void*) > 4,
                "COMMONFILESDIR" => Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFiles),
                "COMPILED" => false,
                "COMPUTERNAME" => Environment.MachineName,
                "COMSPEC" => Environment.GetEnvironmentVariable(NativeInterop.DoPlatformDependent("comspec", "SHELL")),
                "CR" => "\r",
                "CRLF" => Environment.NewLine,
                "CPUARCH" or "osarch" => Environment.Is64BitOperatingSystem ? "X64" : "X86",
                "DESKTOPCOMMONDIR" => Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory),
                "DESKTOPDIR" => Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                "DOCUMENTSCOMMONDIR" => Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments),
                "EXITCODE" => Interpreter.ExitCode,
                "ERROR" => Interpreter.ErrorCode,
                "EXTENDED" => Interpreter.ExtendedValue,
                "FAVORITESCOMMONDIR" => Environment.GetFolderPath(Environment.SpecialFolder.Favorites),
                "FAVORITESDIR" => Environment.GetFolderPath(Environment.SpecialFolder.Favorites),
                "HOMEDRIVE" => new DirectoryInfo(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)).Root.FullName,
                "HOMEPATH" or "userprofiledir" => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "HOUR" => DateTime.Now.ToString("HH", null),
                "LOCALAPPDATADIR" => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "LOGONDOMAIN" => Environment.UserDomainName,
                "LOGONSERVER" => @"\\" + Environment.UserDomainName,
                "MDAY" => DateTime.Now.ToString("dd", null),
                "MIN" => DateTime.Now.ToString("mm", null),
                "MON" => DateTime.Now.ToString("MM", null),
                "MSEC" => DateTime.Now.ToString("fff", null),
                "MYDOCUMENTSDIR" => Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "NUMPARAMS" => (frame as AU3CallFrame)?.PassedArguments.Length ?? 0,
                "MUILANG" or "oslang" => NativeInterop.DoPlatformDependent(NativeInterop.GetUserDefaultUILanguage, () => default),
                "TAB" => "\t",
                "SW_DISABLE" => 65,
                "SW_ENABLE" => 64,
                "SW_HIDE" => 0,
                "SW_LOCK" => 66,
                "SW_MAXIMIZE" => 3,
                "SW_MINIMIZE" => 6,
                "SW_RESTORE" => 9,
                "SW_SHOW" => 5,
                "SW_SHOWDEFAULT" => 10,
                "SW_SHOWMAXIMIZED" => 3,
                "SW_SHOWMINIMIZED" => 2,
                "SW_SHOWMINNOACTIVE" => 7,
                "SW_SHOWNA" => 8,
                "SW_SHOWNOACTIVATE" => 4,
                "SW_SHOWNORMAL" => 1,
                "SW_UNLOCK" => 67,
                "TEMPDIR" => NativeInterop.DoPlatformDependent(Environment.GetEnvironmentVariable("temp"), "/tmp"),
                "OSSERVICEPACK" => _os.ServicePack,
                "OSBUILD" => _os.Version.Build,
                "OSTYPE" => NativeInterop.DoPlatformDependent("WIN32_NT", "LINUX", "MACOS_X"),
                "OSVERSION" => NativeInterop.OperatingSystem switch
                {
                    OS.Windows => (_os.Platform, _os.Version.Major, _os.Version.Minor, NativeInterop.IsWindowsServer()) switch
                    {
                        // https://stackoverflow.com/a/2819962/3902603
                        (PlatformID.WinCE, _, _, _) => "WIN_CE",
                        (PlatformID.Win32S, _, _, _) => "WIN_S",
                        (PlatformID.Win32Windows, 4, 0, _) => "WIN_95",
                        (PlatformID.Win32Windows, 4, 10, _) => "WIN_98",
                        (PlatformID.Win32Windows, 4, 90, _) => "WIN_ME",
                        (PlatformID.Win32NT, 4, _, _) => "WIN_NT4",
                        (PlatformID.Win32NT, 5, 0, _) => "WIN_2000",
                        (PlatformID.Win32NT, 5, 1, _) => "WIN_XP",
                        (PlatformID.Win32NT, 5, 2, _) => "WIN_2003",
                        (PlatformID.Win32NT, 6, 0, false) => "WIN_VISTA",
                        (PlatformID.Win32NT, 6, 0, true) => "WIN_2008",
                        (PlatformID.Win32NT, 6, 1, false) => "WIN_7",
                        (PlatformID.Win32NT, 6, 1, true) => "WIN_2008R2",
                        (PlatformID.Win32NT, 6, 2, false) => "WIN_8",
                        (PlatformID.Win32NT, 6, 2, true) => "WIN_2012",
                        (PlatformID.Win32NT, 6, 3, false) => "WIN_81",
                        (PlatformID.Win32NT, 6, 3, true) => "WIN_2012R2",
                        (PlatformID.Win32NT, 10, 0, false) => "WIN_10",
                        // (PlatformID.Win32NT, 10, 0, true) => "WIN_2016",
                        // (PlatformID.Win32NT, 10, 0, true) => "WIN_2019",
                        _ => "WIN32_NT"
                    },
                    OS.Linux => "LINUX",
                    OS.MacOS => "MACOS_X",
                    OS.Unknown or _ => "UNKNOWN",
                },

                "PROGRAMFILESDIR" => Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "PROGRAMSCOMMONDIR" => Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFiles),
                "PROGRAMSDIR" => Environment.GetFolderPath(Environment.SpecialFolder.Programs),
                "SCRIPTDIR" => Path.GetDirectoryName(location.FullFileName),
                "SCRIPTFULLPATH" => location.FullFileName,
                "SCRIPTLINENUMBER" => location.StartLineNumber,
                "SCRIPTNAME" => Path.GetFileName(location.FullFileName),
                "STARTMENUCOMMONDIR" => Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
                "STARTMENUDIR" => Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
                "STARTUPCOMMONDIR" => Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup),
                "STARTUPDIR" => Environment.GetFolderPath(Environment.SpecialFolder.Startup),
                "SYSTEMDIR" => Environment.GetFolderPath(Environment.SpecialFolder.SystemX86),
                "WINDOWSDIR" => Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                "SEC" => DateTime.Now.ToString("ss", null),
                "USERNAME" => Environment.UserName,
                "YDAY" => DateTime.Now.DayOfYear.ToString("D3", null),
                "YEAR" => DateTime.Now.ToString("yyyy", null),
                "WDAY" => (int)DateTime.Now.DayOfWeek + 1,
                "WORKINGDIR" => Directory.GetCurrentDirectory(),

                "LF" => "\n",

                _ when name.Equals(MACRO_DISCARD, StringComparison.InvariantCultureIgnoreCase) =>
                    frame.VariableResolver.TryGetVariable(VARIABLE.Discard, VariableSearchScope.Global, out Variable? discard) ? discard.Value : Variant.Null,
                _ => (Variant?)null,
            };

            if (value is null && name.Match(REGEX_IPADDRESS, out ReadOnlyIndexer<string, string>? g))
            {
                IPHostEntry host = Dns.GetHostEntry(Dns.GetHostName());
                List<string> ips = new List<string>();
                int idx = (int)decimal.Parse(g["num"], null);

                foreach (IPAddress ip in host.AddressList)
                    if (ip.AddressFamily is AddressFamily.InterNetwork)
                        ips.Add(ip.ToString());

                value = idx < ips.Count ? ips[idx] : "0.0.0.0";
            }

            return value is Variant;
        }
    }

    public sealed class AdditionalMacros
        : AbstractMacroProvider
    {
        public AdditionalMacros(Interpreter interpreter)
            : base(interpreter)
        {
        }

        public override bool ProvideMacroValue(CallFrame frame, string name, out Variant? value) => (value = name.ToUpperInvariant() switch
        {
            "ESC" => "\x1b",
            "VTAB" => "\v",
            "NUL" => "\0",
            "DATE" => DateTime.Now.ToString("yyyy-MM-dd", null),
            "DATE_TIME" => DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.ffffff", null),
            "E" => Math.E,
            "NL" => Environment.NewLine,
            "PHI" => 1.618033988749894848204586834m,
            "PI" => Math.PI,
            "TAU" => Math.Tau,
            _ => (Variant?)null,
        }) is Variant;
    }
}
