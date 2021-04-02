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


        public unsafe FrameworkMacros(Interpreter interpreter)
            : base(interpreter)
        {
            RegisterMacro(MACRO_DISCARD, f => f.VariableResolver.TryGetVariable(VARIABLE.Discard, VariableSearchScope.Global, out Variable? discard) ? discard.Value : Variant.Null);
            RegisterMacro("APPDATACOMMONDIR", Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData));
            RegisterMacro("APPDATADIR", Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));
            RegisterMacro("AUTOITEXE", _ => MainProgram.ASM_FILE.FullName);
            RegisterMacro("AUTOITPID", _ => Environment.ProcessId);
            RegisterMacro("AUTOITVERSION", _ => __module__.InterpreterVersion?.ToString() ?? "0.0.0.0");
            RegisterMacro("AUTOITX64", _ => sizeof(void*) > 4);
            RegisterMacro("COMMONFILESDIR", _ => Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFiles));
            RegisterMacro("COMPILED", false);
            RegisterMacro("COMPUTERNAME", _ => Environment.MachineName);
            RegisterMacro("COMSPEC", _ => Environment.GetEnvironmentVariable(NativeInterop.DoPlatformDependent("comspec", "SHELL")));
            RegisterMacro("CR", "\r");
            RegisterMacro("CRLF", Environment.NewLine);
            RegisterMacro("CPUARCH", Environment.Is64BitOperatingSystem ? "X64" : "X86");
            RegisterMacro("osarch", Environment.Is64BitOperatingSystem ? "X64" : "X86");
            RegisterMacro("DESKTOPCOMMONDIR", Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory));
            RegisterMacro("DESKTOPDIR", Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory));
            RegisterMacro("DOCUMENTSCOMMONDIR", Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments));
            RegisterMacro("EXITCODE", f => f.Interpreter.ExitCode);
            RegisterMacro("EXITMETHOD", f => (int)f.Interpreter.ExitMethod);
            RegisterMacro("ERROR", f => f.Interpreter.ErrorCode);
            RegisterMacro("EXTENDED", f => f.Interpreter.ExtendedValue);
            RegisterMacro("FAVORITESCOMMONDIR", Environment.GetFolderPath(Environment.SpecialFolder.Favorites));
            RegisterMacro("FAVORITESDIR", Environment.GetFolderPath(Environment.SpecialFolder.Favorites));
            RegisterMacro("HOMEDRIVE", _ => new DirectoryInfo(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)).Root.FullName);
            RegisterMacro("HOMEPATH", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
            RegisterMacro("USERPROFILEDIR", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
            RegisterMacro("HOUR", _ => DateTime.Now.ToString("HH", null));
            RegisterMacro("LF", "\n");
            RegisterMacro("LOCALAPPDATADIR", Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
            RegisterMacro("LOGONDOMAIN", _ => Environment.UserDomainName);
            RegisterMacro("LOGONSERVER", _ => @"\\" + Environment.UserDomainName);
            RegisterMacro("MDAY", _ => DateTime.Now.ToString("dd", null));
            RegisterMacro("MIN", _ => DateTime.Now.ToString("mm", null));
            RegisterMacro("MON", _ => DateTime.Now.ToString("MM", null));
            RegisterMacro("MSEC", _ => DateTime.Now.ToString("fff", null));
            RegisterMacro("MYDOCUMENTSDIR", _ => Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
            RegisterMacro("NUMPARAMS", f => (f as AU3CallFrame)?.PassedArguments.Length ?? 0);
            RegisterMacro("MUILANG", _ => NativeInterop.DoPlatformDependent(NativeInterop.GetUserDefaultUILanguage, () => default));
            RegisterMacro("OSLANG", _ => NativeInterop.DoPlatformDependent(NativeInterop.GetUserDefaultUILanguage, () => default));
            RegisterMacro("TAB", "\t");
            RegisterMacro("SW_DISABLE", 65);
            RegisterMacro("SW_ENABLE", 64);
            RegisterMacro("SW_HIDE", 0);
            RegisterMacro("SW_LOCK", 66);
            RegisterMacro("SW_MAXIMIZE", 3);
            RegisterMacro("SW_MINIMIZE", 6);
            RegisterMacro("SW_RESTORE", 9);
            RegisterMacro("SW_SHOW", 5);
            RegisterMacro("SW_SHOWDEFAULT", 10);
            RegisterMacro("SW_SHOWMAXIMIZED", 3);
            RegisterMacro("SW_SHOWMINIMIZED", 2);
            RegisterMacro("SW_SHOWMINNOACTIVE", 7);
            RegisterMacro("SW_SHOWNA", 8);
            RegisterMacro("SW_SHOWNOACTIVATE", 4);
            RegisterMacro("SW_SHOWNORMAL", 1);
            RegisterMacro("SW_UNLOCK", 67);
            RegisterMacro("TEMPDIR", _ => NativeInterop.DoPlatformDependent(Environment.GetEnvironmentVariable("temp"), "/tmp"));
            RegisterMacro("OSSERVICEPACK", _os.ServicePack);
            RegisterMacro("OSBUILD", _os.Version.Build);
            RegisterMacro("OSTYPE", NativeInterop.DoPlatformDependent("WIN32_NT", "LINUX", "MACOS_X"));
            RegisterMacro("OSVERSION", NativeInterop.OperatingSystem switch
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
            });
            RegisterMacro("PROGRAMFILESDIR", Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles));
            RegisterMacro("PROGRAMSCOMMONDIR", Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFiles));
            RegisterMacro("PROGRAMSDIR", Environment.GetFolderPath(Environment.SpecialFolder.Programs));
            RegisterMacro("SCRIPTDIR", f => Path.GetDirectoryName(GetLocation(f).FullFileName));
            RegisterMacro("SCRIPTFULLPATH", f => GetLocation(f).FullFileName);
            RegisterMacro("SCRIPTLINENUMBER", f => GetLocation(f).StartLineNumber);
            RegisterMacro("SCRIPTNAME", f => Path.GetFileName(GetLocation(f).FullFileName));
            RegisterMacro("STARTMENUCOMMONDIR", Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu));
            RegisterMacro("STARTMENUDIR", Environment.GetFolderPath(Environment.SpecialFolder.StartMenu));
            RegisterMacro("STARTUPCOMMONDIR", Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup));
            RegisterMacro("STARTUPDIR", Environment.GetFolderPath(Environment.SpecialFolder.Startup));
            RegisterMacro("SYSTEMDIR", Environment.GetFolderPath(Environment.SpecialFolder.SystemX86));
            RegisterMacro("TRAY_ID", Variant.Null);
            RegisterMacro("TRAYICONFLASHING", Variant.False);
            RegisterMacro("TRAYICONVISIBLE", Variant.False);
            RegisterMacro("WINDOWSDIR", Environment.GetFolderPath(Environment.SpecialFolder.Windows));
            RegisterMacro("SEC", _ => DateTime.Now.ToString("ss", null));
            RegisterMacro("USERNAME", _ => Environment.UserName);
            RegisterMacro("YDAY", _ => DateTime.Now.DayOfYear.ToString("D3", null));
            RegisterMacro("YEAR", _ => DateTime.Now.ToString("yyyy", null));
            RegisterMacro("WDAY", _ => (int)DateTime.Now.DayOfWeek + 1);
            RegisterMacro("WORKINGDIR", _ => Directory.GetCurrentDirectory());
        }

        private static SourceLocation GetLocation(CallFrame frame) => frame.CurrentThread.CurrentLocation ?? frame.Interpreter.MainThread?.CurrentLocation ?? SourceLocation.Unknown;

        public unsafe override bool ProvideMacroValue(CallFrame frame, string name, out (Variant value, Metadata metadata)? macro)
        {
            if (base.ProvideMacroValue(frame, name, out macro))
                return true;
            else if (name.Match(REGEX_IPADDRESS, out ReadOnlyIndexer<string, string>? g))
            {
                IPHostEntry host = Dns.GetHostEntry(Dns.GetHostName());
                List<string> ips = new();
                int idx = (int)double.Parse(g["num"], null);

                foreach (IPAddress ip in host.AddressList)
                    if (ip.AddressFamily is AddressFamily.InterNetwork)
                        ips.Add(ip.ToString());

                macro = (idx < ips.Count ? ips[idx] : "0.0.0.0", Metadata.Default);

                return true;
            }
            else
                return false;
        }
    }

    public sealed class AdditionalMacros
        : AbstractKnownMacroProvider
    {
        public AdditionalMacros(Interpreter interpreter)
            : base(interpreter)
        {
            Random random = new();

            RegisterMacro("ESC", "\x1b");
            RegisterMacro("VTAB", "\v");
            RegisterMacro("NUL", "\0");
            RegisterMacro("DATE", _ => DateTime.Now.ToString("yyyy-MM-dd", null));
            RegisterMacro("DATE_TIME", _ => DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.ffffff", null));
            RegisterMacro("E", Math.E);
            RegisterMacro("NL", Environment.NewLine);
            RegisterMacro("PHI", 1.618033988749894848204586834m);
            RegisterMacro("PI", Math.PI);
            RegisterMacro("TAU", Math.Tau);
            RegisterMacro("RANDOM", _ => random.NextDouble());
        }
    }
}
