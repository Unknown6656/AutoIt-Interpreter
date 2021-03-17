using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Net;
using System.IO;
using System;

using Unknown6656.AutoIt3.Parser.ExpressionParser;
using Unknown6656.AutoIt3.Runtime.Native;
using Unknown6656.AutoIt3.Runtime;
using Unknown6656.AutoIt3.CLI;
using Unknown6656.Common;

namespace Unknown6656.AutoIt3.Extensibility.Plugins.Au3Framework
{
    using static AST;

    public sealed class FrameworkMacros
        : AbstractKnownMacroProvider
    {
        internal const string MACRO_DISCARD = "DISCARD";
        private static readonly Regex REGEX_IPADDRESS = new(@"ipaddress(?<num>\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly OperatingSystem _os = Environment.OSVersion;


        public override unsafe Dictionary<string, Func<CallFrame, Variant>> KnownMacros { get; } = new()
        {
            [MACRO_DISCARD] = f => f.VariableResolver.TryGetVariable(VARIABLE.Discard, VariableSearchScope.Global, out Variable? discard) ? discard.Value : Variant.Null,
            ["APPDATACOMMONDIR"] = _ => Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            ["APPDATADIR"] = _ => Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            ["AUTOITEXE"] = _ => MainProgram.ASM_FILE.FullName,
            ["AUTOITPID"] = _ => Environment.ProcessId,
            ["AUTOITVERSION"] = _ => __module__.InterpreterVersion?.ToString() ?? "0.0.0.0",
            ["AUTOITX64"] = _ => sizeof(void*) > 4,
            ["COMMONFILESDIR"] = _ => Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFiles),
            ["COMPILED"] = _ => false,
            ["COMPUTERNAME"] = _ => Environment.MachineName,
            ["COMSPEC"] = _ => Environment.GetEnvironmentVariable(NativeInterop.DoPlatformDependent("comspec", "SHELL")),
            ["CR"] = _ => "\r",
            ["CRLF"] = _ => Environment.NewLine,
            ["CPUARCH"] = _ => Environment.Is64BitOperatingSystem ? "X64" : "X86",
            ["osarch"] = _ => Environment.Is64BitOperatingSystem ? "X64" : "X86",
            ["DESKTOPCOMMONDIR"] = _ => Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory),
            ["DESKTOPDIR"] = _ => Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            ["DOCUMENTSCOMMONDIR"] = _ => Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments),
            ["EXITCODE"] = f => f.Interpreter.ExitCode,
            ["ERROR"] = f => f.Interpreter.ErrorCode,
            ["EXTENDED"] = f => f.Interpreter.ExtendedValue,
            ["FAVORITESCOMMONDIR"] = _ => Environment.GetFolderPath(Environment.SpecialFolder.Favorites),
            ["FAVORITESDIR"] = _ => Environment.GetFolderPath(Environment.SpecialFolder.Favorites),
            ["HOMEDRIVE"] = _ => new DirectoryInfo(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)).Root.FullName,
            ["HOMEPATH"] = _ => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ["USERPROFILEDIR"] = _ => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ["HOUR"] = _ => DateTime.Now.ToString("HH", null),
            ["LF"] = _ => "\n",
            ["LOCALAPPDATADIR"] = _ => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            ["LOGONDOMAIN"] = _ => Environment.UserDomainName,
            ["LOGONSERVER"] = _ => @"\\" + Environment.UserDomainName,
            ["MDAY"] = _ => DateTime.Now.ToString("dd", null),
            ["MIN"] = _ => DateTime.Now.ToString("mm", null),
            ["MON"] = _ => DateTime.Now.ToString("MM", null),
            ["MSEC"] = _ => DateTime.Now.ToString("fff", null),
            ["MYDOCUMENTSDIR"] = _ => Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            ["NUMPARAMS"] = f => (f as AU3CallFrame)?.PassedArguments.Length ?? 0,
            ["MUILANG"] = _ => NativeInterop.DoPlatformDependent(NativeInterop.GetUserDefaultUILanguage, () => default),
            ["OSLANG"] = _ => NativeInterop.DoPlatformDependent(NativeInterop.GetUserDefaultUILanguage, () => default),
            ["TAB"] = _ => "\t",
            ["SW_DISABLE"] = _ => 65,
            ["SW_ENABLE"] = _ => 64,
            ["SW_HIDE"] = _ => 0,
            ["SW_LOCK"] = _ => 66,
            ["SW_MAXIMIZE"] = _ => 3,
            ["SW_MINIMIZE"] = _ => 6,
            ["SW_RESTORE"] = _ => 9,
            ["SW_SHOW"] = _ => 5,
            ["SW_SHOWDEFAULT"] = _ => 10,
            ["SW_SHOWMAXIMIZED"] = _ => 3,
            ["SW_SHOWMINIMIZED"] = _ => 2,
            ["SW_SHOWMINNOACTIVE"] = _ => 7,
            ["SW_SHOWNA"] = _ => 8,
            ["SW_SHOWNOACTIVATE"] = _ => 4,
            ["SW_SHOWNORMAL"] = _ => 1,
            ["SW_UNLOCK"] = _ => 67,
            ["TEMPDIR"] = _ => NativeInterop.DoPlatformDependent(Environment.GetEnvironmentVariable("temp"), "/tmp"),
            ["OSSERVICEPACK"] = _ => _os.ServicePack,
            ["OSBUILD"] = _ => _os.Version.Build,
            ["OSTYPE"] = _ => NativeInterop.DoPlatformDependent("WIN32_NT", "LINUX", "MACOS_X"),
            ["OSVERSION"] = _ => NativeInterop.OperatingSystem switch
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
            ["PROGRAMFILESDIR"] = _ => Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            ["PROGRAMSCOMMONDIR"] = _ => Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFiles),
            ["PROGRAMSDIR"] = _ => Environment.GetFolderPath(Environment.SpecialFolder.Programs),
            ["SCRIPTDIR"] = f => Path.GetDirectoryName(GetLocation(f).FullFileName),
            ["SCRIPTFULLPATH"] = f => GetLocation(f).FullFileName,
            ["SCRIPTLINENUMBER"] = f => GetLocation(f).StartLineNumber,
            ["SCRIPTNAME"] = f => Path.GetFileName(GetLocation(f).FullFileName),
            ["STARTMENUCOMMONDIR"] = _ => Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
            ["STARTMENUDIR"] = _ => Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
            ["STARTUPCOMMONDIR"] = _ => Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup),
            ["STARTUPDIR"] = _ => Environment.GetFolderPath(Environment.SpecialFolder.Startup),
            ["SYSTEMDIR"] = _ => Environment.GetFolderPath(Environment.SpecialFolder.SystemX86),
            ["WINDOWSDIR"] = _ => Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            ["SEC"] = _ => DateTime.Now.ToString("ss", null),
            ["USERNAME"] = _ => Environment.UserName,
            ["YDAY"] = _ => DateTime.Now.DayOfYear.ToString("D3", null),
            ["YEAR"] = _ => DateTime.Now.ToString("yyyy", null),
            ["WDAY"] = _ => (int)DateTime.Now.DayOfWeek + 1,
            ["WORKINGDIR"] = _ => Directory.GetCurrentDirectory(),
        };


        public FrameworkMacros(Interpreter interpreter)
            : base(interpreter)
        {
        }

        private static SourceLocation GetLocation(CallFrame frame) => frame.CurrentThread.CurrentLocation ?? frame.Interpreter.MainThread?.CurrentLocation ?? SourceLocation.Unknown;

        public unsafe override bool ProvideMacroValue(CallFrame frame, string name, out Variant? value)
        {
            if (base.ProvideMacroValue(frame, name, out value))
                return true;
            else if (name.Match(REGEX_IPADDRESS, out ReadOnlyIndexer<string, string>? g))
            {
                IPHostEntry host = Dns.GetHostEntry(Dns.GetHostName());
                List<string> ips = new();
                int idx = (int)decimal.Parse(g["num"], null);

                foreach (IPAddress ip in host.AddressList)
                    if (ip.AddressFamily is AddressFamily.InterNetwork)
                        ips.Add(ip.ToString());

                value = idx < ips.Count ? ips[idx] : "0.0.0.0";

                return true;
            }
            else
                return false;
        }
    }

    public sealed class AdditionalMacros
        : AbstractKnownMacroProvider
    {
        public override Dictionary<string, Func<CallFrame, Variant>> KnownMacros { get; } = new()
        {
            ["ESC"] = _ => "\x1b",
            ["VTAB"] = _ => "\v",
            ["NUL"] = _ => "\0",
            ["DATE"] = _ => DateTime.Now.ToString("yyyy-MM-dd", null),
            ["DATE_TIME"] = _ => DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.ffffff", null),
            ["E"] = _ => Math.E,
            ["NL"] = _ => Environment.NewLine,
            ["PHI"] = _ => 1.618033988749894848204586834m,
            ["PI"] = _ => Math.PI,
            ["TAU"] = _ => Math.Tau,
        };


        public AdditionalMacros(Interpreter interpreter)
            : base(interpreter)
        {
        }
    }
}
