using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Sockets;
using System.Net;
using System.IO;
using System;

using Unknown6656.AutoIt3.ExpressionParser;
using Unknown6656.AutoIt3.Runtime;
using Unknown6656.Common;

namespace Unknown6656.AutoIt3.Extensibility.Plugins.Au3Framework
{
    using static Program;
    using static AST;

    public sealed class FrameworkMacros
        : AbstractMacroProvider
    {
        internal const string MACRO_DISCARD = "DISCARD";


        public FrameworkMacros(Interpreter interpreter)
            : base(interpreter)
        {
        }

        public unsafe override bool ProvideMacroValue(CallFrame frame, string name, out Variant? value)
        {
            SourceLocation location = frame.CurrentThread.CurrentLocation ?? Interpreter.MainThread?.CurrentLocation ?? SourceLocation.Unknown;

            value = name.ToLowerInvariant() switch
            {
                "appdatacommondir" => Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "appdatadir" => Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "autoitexe" => ASM.FullName,
                "autoitpid" => Process.GetCurrentProcess().Id,
                "autoitversion" => __module__.InterpreterVersion?.ToString() ?? "0.0.0.0",
                "autoitx64" => sizeof(void*) > 4,
                "commonfilesdir" => Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFiles),
                "compiled" => false,
                "computername" => Environment.MachineName,
                "comspec" => Environment.GetEnvironmentVariable(Interpreter.OperatingSystem == Runtime.OperatingSystem.Windows ? "comspec" : "SHELL"),
                "cr" => "\r",
                "crlf" => Environment.NewLine,
                "cpuarch" or "osarch" => Environment.Is64BitOperatingSystem ? "X64" : "X86",
                "desktopcommondir" => Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory),
                "desktopdir" => Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                "documentscommondir" => Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments),
                "exitcode" => Interpreter.ExitCode,
                "favoritescommondir" => Environment.GetFolderPath(Environment.SpecialFolder.Favorites),
                "favoritesdir" => Environment.GetFolderPath(Environment.SpecialFolder.Favorites),
                "homedrive" => new DirectoryInfo(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)).Root.FullName,
                "homepath" or "userprofiledir" => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "hour" => DateTime.Now.ToString("HH"),
                "localappdatadir" => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "logondomain" => Environment.UserDomainName,
                "logonserver" => @"\\" + Environment.UserDomainName,
                "mday" => DateTime.Now.ToString("dd"),
                "min" => DateTime.Now.ToString("mm"),
                "mon" => DateTime.Now.ToString("MM"),
                "msec" => DateTime.Now.ToString("fff"),
                "mydocumentsdir" => Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "numparams" => (frame as AU3CallFrame)?.PassedArguments.Length ?? 0,
                "tab" => "\t",
                "sw_disable" => 65,
                "sw_enable" => 64,
                "sw_hide" => 0,
                "sw_lock" => 66,
                "sw_maximize" => 3,
                "sw_minimize" => 6,
                "sw_restore" => 9,
                "sw_show" => 5,
                "sw_showdefault" => 10,
                "sw_showmaximized" => 3,
                "sw_showminimized" => 2,
                "sw_showminnoactive" => 7,
                "sw_showna" => 8,
                "sw_shownoactivate" => 4,
                "sw_shownormal" => 1,
                "sw_unlock" => 67,
                "tempdir" => Interpreter.OperatingSystem == Runtime.OperatingSystem.Windows ? Environment.GetEnvironmentVariable("temp") : "/tmp",

                "osbuild" => Environment.OSVersion.Version.Build,

                "programfilesdir" => Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "programscommondir" => Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFiles),
                "programsdir" => Environment.GetFolderPath(Environment.SpecialFolder.Programs),
                "scriptdir" => location.FileName.Directory?.FullName,
                "scriptfullpath" => location.FileName.FullName,
                "scriptlinenumber" => location.StartLineNumber,
                "scriptname" => location.FileName.Name,
                "startmenucommondir" => Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
                "startmenudir" => Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
                "startupcommondir" => Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup),
                "startupdir" => Environment.GetFolderPath(Environment.SpecialFolder.Startup),
                "systemdir" => Environment.GetFolderPath(Environment.SpecialFolder.SystemX86),
                "windowsdir" => Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                "sec" => DateTime.Now.ToString("ss"),
                "username" => Environment.UserName,
                "yday" => DateTime.Now.DayOfYear.ToString("D3"),
                "year" => DateTime.Now.ToString("yyyy"),
                "wday" => (int)DateTime.Now.DayOfWeek + 1,
                "workingdir" => Directory.GetCurrentDirectory(),

                "lf" => "\n",

                _ when name.Equals(MACRO_DISCARD, StringComparison.InvariantCultureIgnoreCase) => frame.VariableResolver.TryGetVariable(VARIABLE.Discard, out Variable? discard) ? discard.Value : Variant.Null,
                _ => (Variant?)null,
            };

            if (value is null && name.Match(@"ipaddress(?<num>\d+)", out Match m))
            {
                IPHostEntry host = Dns.GetHostEntry(Dns.GetHostName());
                List<string> ips = new List<string>();
                int idx = (int)decimal.Parse(m.Groups["num"].Value);

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

        public override bool ProvideMacroValue(CallFrame frame, string name, out Variant? value) => (value = name.ToLowerInvariant() switch
        {
            "esc" => "\x1b",
            "nul" => "\0",
            _ => (Variant?)null,
        }) is Variant;
    }
}
