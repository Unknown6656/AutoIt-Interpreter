using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.IO.MemoryMappedFiles;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Globalization;
using System.Diagnostics;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using System.Linq;
using System.Text;
using System.Net;
using System.IO;
using System;

using Renci.SshNet;

using SixLabors.ImageSharp.Processing.Transforms;
using SixLabors.ImageSharp.Processing.Filters;
using SixLabors.ImageSharp.Processing.Effects;
using SixLabors.ImageSharp.Processing.Drawing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp;

namespace AutoItCoreLibrary
{
    using static Win32;

    using var = AutoItVariantType;
    using Bitmap = Image<Argb32>;


#pragma warning disable RCS1047, RCS1057, IDE1006

    public static unsafe class AutoItFunctions
    {
        public const string FUNC_PREFIX = "__userfunc_";
        public const string PINVOKE_PREFIX = "__pinvoke_";
        public const string MMF_CMDRARG = "____input_shared_memory_mapped_file____";
        public const string MMF_CMDPARG = "____output_shared_memory_mapped_file____";
        public const string DBG_CMDARG =  "____attach_debugger____";

        private static var __error;
        private static var __extended;

        public static bool LittleEndian { get; }
        public static Assembly ScriptAssembly { get; }
        public static AutoItMacroDictionary StaticMacros { get; } = new AutoItMacroDictionary(s =>
        {
            switch (s.ToLower().Trim())
            {
                #region AutoIt3 Specification

                case "appdatacommondir":
                    return Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                case "appdatadir":
                    return Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                case "autoitexe":
                    return ScriptAssembly.Location;
                case "autoitpid":
                    return Process.GetCurrentProcess().Id;
                case "autoitversion":
                    return "3.42.++";
                case "autoitx64":
                    return Is64Bit ? 1 : 0;
                case "com_eventobj": // TODO: object the com event is being fired on. only valid in a com event function.
                    break;
                case "commonfilesdir":
                    return Environment.GetFolderPath(Is64Bit ? Environment.SpecialFolder.CommonProgramFiles : Environment.SpecialFolder.CommonProgramFilesX86);
                case "compiled":
                    return 1;
                case "computername":
                    return Environment.MachineName;
                case "comspec":
                    return GetPlatformSpecific(
                        () => Environment.ExpandEnvironmentVariables("%COMSPEC%"),
                        () => Environment.ExpandEnvironmentVariables("$SHELL")
                    );
                case "cpuarch":
                    return Is64Bit ? "x64" : "x86";
                case "cr":
                    return "\r";
                case "crlf":
                    return "\r\n";
                case "desktopcommondir":
                    return Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);
                case "desktopdepth": // TODO :
                    break;
                case "desktopdir":
                    return Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                case "desktopheight": // TODO :
                case "desktoprefresh": // TODO :
                case "desktopwidth": // TODO :
                    break;
                case "documentscommondir":
                    return Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments);
                case "error":
                    return __error;
                case "exitcode":
                    return Environment.ExitCode;
                case "exitmethod": // TODO :
                    break;
                case "extended":
                    return __extended;
                case "favoritescommondir":
                case "favoritesdir":
                    return Environment.GetFolderPath(Environment.SpecialFolder.Favorites);
                case "gui_ctrlhandle": // TODO :
                case "gui_ctrlid": // TODO :
                case "gui_dragfile": // TODO :
                case "gui_dragid": // TODO :
                case "gui_dropid": // TODO :
                case "gui_winhandle": // TODO :
                    break;
                case "homedrive":
                    return GetPlatformSpecific(
                        () => Environment.ExpandEnvironmentVariables("%HOMEDRIVE%"),
                        () => "/"
                    );
                case "homepath":
                    return GetPlatformSpecific(
                        () => Environment.ExpandEnvironmentVariables("%HOMEPATH%"),
                        () => Environment.ExpandEnvironmentVariables("$HOME")
                    );
                case "homeshare":
                    return $@"\\{Environment.MachineName}\{Environment.ExpandEnvironmentVariables("%HOMEDRIVE%").Replace(':', '$')}{Environment.ExpandEnvironmentVariables("%HOMEPATH%")}";
                case "hotkeypressed":
                    return null;
                case "hour":
                    return DateTime.Now.Hour.ToString("D2");
                case "ipaddress1": // TODO: ip address of first network adapter. tends to return 127.0.0.1 on some computers.
                case "ipaddress2": // TODO: ip address of second network adapter. returns 0.0.0.0 if not applicable.
                case "ipaddress3": // TODO: ip address of third network adapter. returns 0.0.0.0 if not applicable.
                case "ipaddress4": // TODO: ip address of fourth network adapter. returns 0.0.0.0 if not applicable.
                    break;
                case "kblayout":
                    return GetPlatformSpecific(
                        () => ((long)GetKeyboardLayout(Thread.CurrentThread.ManagedThreadId)).ToString("x8"),
                        () => null // TODO
                    );
                case "lf":
                    return "\n";
                case "localappdatadir":
                    return Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                case "logondnsdomain": // TODO: logon dns domain.
                    break;
                case "logondomain":
                    return Environment.UserDomainName;
                case "logonserver": // TODO: logon server.
                    break;
                case "mday":
                    return DateTime.Now.Day.ToString("D2");
                case "min":
                    return DateTime.Now.Minute.ToString("D2");
                case "mon":
                    return DateTime.Now.Month.ToString("D2");
                case "msec":
                    return DateTime.Now.Millisecond.ToString("D3");
                case "muilang": // TODO: returns code denoting multi language if available (vista is ok by default). see appendix for possible values.
                    break;
                case "mydocumentsdir":
                    return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                case "numparams": // TODO: number of parameters used in calling the user function.
                    break;
                case "osarch":
                    return Environment.Is64BitOperatingSystem ? "X64" : "X86";
                case "osbuild":
                    return Environment.OSVersion.Version.Build;
                case "oslang":
                    return CultureInfo.CurrentCulture.LCID.ToString("x4");
                case "osservicepack":
                    return Environment.OSVersion.ServicePack;
                case "ostype":
                    return Environment.OSVersion.Platform.ToString();
                case "osversion": // TODO: returns one of the following: "win_10", "win_81", "win_8", "win_7", "win_vista", "win_xp", "win_xpe",  for windows servers: "win_2016", "win_2012r2", "win_2012", "win_2008r2", "win_2008", "win_2003"".
                    break;
                case "programfilesdir":
                    return Environment.GetFolderPath(Is64Bit ? Environment.SpecialFolder.ProgramFiles : Environment.SpecialFolder.ProgramFilesX86);
                case "programscommondir":
                    return Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms);
                case "programsdir":
                    return Environment.GetFolderPath(Environment.SpecialFolder.Programs);
                case "scriptdir":
                    return new FileInfo(ScriptAssembly.Location).Directory.FullName;
                case "scriptfullpath":
                    return new FileInfo(ScriptAssembly.Location).FullName;
                case "scriptlinenumber": // TODO: line number being executed - useful for debug statements (e.g. location of function call). only significant in uncompiled scripts - note that #include files return their internal line numbering
                    break;
                case "scriptname":
                    return new FileInfo(ScriptAssembly.Location).Name;
                case "sec":
                    return DateTime.Now.Second.ToString("D2");
                case "startmenucommondir":
                    return Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu);
                case "startmenudir":
                    return Environment.GetFolderPath(Environment.SpecialFolder.StartMenu);
                case "startupcommondir":
                    return Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup);
                case "startupdir":
                    return Environment.GetFolderPath(Environment.SpecialFolder.Startup);
                case "sw_disable":
                    return 65;
                case "sw_enable":
                    return 64;
                case "sw_hide":
                    return 0;
                case "sw_lock":
                    return 66;
                case "sw_maximize":
                    return 3;
                case "sw_minimize":
                    return 6;
                case "sw_restore":
                    return 9;
                case "sw_show":
                    return 5;
                case "sw_showdefault":
                    return 10;
                case "sw_showmaximized":
                    return 3;
                case "sw_showminimized":
                    return 2;
                case "sw_showminnoactive":
                    return 7;
                case "sw_showna":
                    return 8;
                case "sw_shownoactivate":
                    return 4;
                case "sw_shownormal":
                    return 1;
                case "sw_unlock":
                    return 67;
                case "systemdir":
                    return Environment.GetFolderPath(Environment.SpecialFolder.System);
                case "systemdirx86":
                    return Environment.GetFolderPath(Environment.SpecialFolder.SystemX86);
                case "tab":
                    return "\t";
                case "tempdir":
                    return GetPlatformSpecific(
                        () => Environment.ExpandEnvironmentVariables("%TEMP%"),
                        () => "/var/tmp",
                        () => Environment.ExpandEnvironmentVariables("$HOME")
                    );
                case "tray_id": // TODO: last clicked item identifier during a traysetonevent() or trayitemsetonevent() action.
                case "trayiconflashing": // TODO: returns 1 if tray icon is flashing; otherwise, returns 0.
                case "trayiconvisible": // TODO: returns 1 if tray icon is visible; otherwise, returns 0.
                    break;
                case "username":
                    return Environment.UserName;
                case "userprofiledir":
                    return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                case "wday":
                    return ((int)DateTime.Now.DayOfWeek + 1).ToString();
                case "windowsdir":
                    return Environment.GetFolderPath(Environment.SpecialFolder.Windows);
                case "workingdir":
                    return Directory.GetCurrentDirectory();
                case "yday":
                    return DateTime.Now.DayOfYear.ToString("D3");
                case "year":
                    return DateTime.Now.Year.ToString("D4");

                #endregion
                #region Additional

                case "ccl_black":
                    return (int)ConsoleColor.Black;
                case "ccl_darkblue":
                    return (int)ConsoleColor.DarkBlue;
                case "ccl_darkgreen":
                    return (int)ConsoleColor.DarkGreen;
                case "ccl_darkcyan":
                    return (int)ConsoleColor.DarkCyan;
                case "ccl_darkred":
                    return (int)ConsoleColor.DarkRed;
                case "ccl_darkmagenta":
                    return (int)ConsoleColor.DarkMagenta;
                case "ccl_darkyellow":
                    return (int)ConsoleColor.DarkYellow;
                case "ccl_gray":
                    return (int)ConsoleColor.Gray;
                case "ccl_darkgray":
                    return (int)ConsoleColor.DarkGray;
                case "ccl_blue":
                    return (int)ConsoleColor.Blue;
                case "ccl_green":
                    return (int)ConsoleColor.Green;
                case "ccl_cyan":
                    return (int)ConsoleColor.Cyan;
                case "ccl_red":
                    return (int)ConsoleColor.Red;
                case "ccl_magenta":
                    return (int)ConsoleColor.Magenta;
                case "ccl_yellow":
                    return (int)ConsoleColor.Yellow;
                case "ccl_white":
                    return (int)ConsoleColor.Black;
                case "date":
                    return DateTime.Now.ToString("yyyy-MM-dd");
                case "date_time":
                    return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.ffffff");
                case "e":
                    return (var)Math.E;
                case "nl":
                    return Environment.NewLine;
                case "null":
                    return "\0";
                case "phi":
                    return 1.618033988749894848204586834m;
                case "pi":
                    return (var)Math.PI;
                case "rgx_none":
                    return (int)RegexOptions.None;
                case "rgx_ignorecase":
                    return (int)RegexOptions.IgnoreCase;
                case "rgx_multiline":
                    return (int)RegexOptions.Multiline;
                case "rgx_explicit":
                    return (int)RegexOptions.ExplicitCapture;
                case "rgx_compiled":
                    return (int)RegexOptions.Compiled;
                case "rgx_singleline":
                    return (int)RegexOptions.Singleline;
                case "rgx_ignorepatternws":
                    return (int)RegexOptions.IgnorePatternWhitespace;
                case "rgx_rtl":
                    return (int)RegexOptions.RightToLeft;
                case "rgx_ecma":
                    return (int)RegexOptions.ECMAScript;
                case "rgx_invariantculture":
                    return (int)RegexOptions.CultureInvariant;
                case "time":
                    return DateTime.Now.ToString("HH:mm:ss");
                case "time_l":
                    return DateTime.Now.ToString("HH:mm:ss.ffffff");
                case "vtab":
                    return "\v";

                #endregion
            }

            return null;
        });


        static AutoItFunctions()
        {
            uint _ = 0xffdead00;

            LittleEndian = *((byte*)&_) == 0x00;

            Assembly casm = typeof(AutoItFunctions).Assembly;

            ScriptAssembly = (from frame in new StackTrace(1).GetFrames()
                              let fasm = frame.GetMethod().DeclaringType.Assembly
                              where fasm != casm
                              select fasm).FirstOrDefault();

            if (ScriptAssembly is null)
                ScriptAssembly = Assembly.LoadFrom(Process.GetCurrentProcess().MainModule.FileName);
        }

        #region helper fuctions

        private static var __(Action f)
        {
            f?.Invoke();

            return var.Empty;
        }

        public static var __InvalidFunction__(params var[] _) =>
            throw new InvalidProgramException("The application tried to call an non-existing function ...");

        public static var DebugPrint(AutoItVariableDictionary vardic) => __(() =>
        {
            Console.WriteLine("globals:");

            foreach (string var in vardic._globals.Keys)
                Console.WriteLine($"    ${var} = \"{vardic._globals[var].ToDebugString()}\"");

            if (vardic._locals.Count > 0)
            {
                Dictionary<string, var> topframe = vardic._locals.Peek();

                Console.WriteLine("locals:");

                foreach (string var in topframe.Keys)
                    Console.WriteLine($"    ${var} = \"{topframe[var].ToDebugString()}\"");
            }
        });

        public static uint ReverseEndianess(uint v) => (v & 0x000000ffu) << 24
                                                     | (v & 0x0000ff00u) << 8
                                                     | (v & 0x00ff0000u) >> 8
                                                     | (v & 0xff000000u) >> 24;

        private static void SetError(var err, var? ext = null) => (__error, __extended) = (err, ext ?? __extended);

        private static void ExecutePlatformSpecific(Action win32, Action posix) => ExecutePlatformSpecific(win32, posix, posix);

        private static void ExecutePlatformSpecific(Action windows, Action linux, Action macosx) =>
            (System == OS.Windows ? windows : System == OS.Linux ? linux : macosx)?.Invoke();

        private static T GetPlatformSpecific<T>(Func<T> win32, Func<T> posix) => GetPlatformSpecific(win32, posix, posix);

        private static T GetPlatformSpecific<T>(Func<T> windows, Func<T> linux, Func<T> macosx) =>
            ((System == OS.Windows ? windows : System == OS.Linux ? linux : macosx) ?? new Func<T>(() => default)).Invoke();

        private static var Try(Action f)
        {
            try
            {
                f();

                return true;
            }
            catch
            {
                return false;
            }
        }

        public static string GeneratePInvokeWrapperName(string dll, string func)
        {
            StringBuilder sb = new StringBuilder();

            foreach (char c in $"{PINVOKE_PREFIX}_{dll}__{func}")
                sb.Append(char.IsLetterOrDigit(c) ? c : '_');

            return sb.ToString();
        }

        private static void SSHCommand(var conn, string cmd) => conn.UseGCHandledData<SshClient>(c => c.RunCommand(cmd).Dispose());

        private static var bitmap_use(this var bmp, Action<Bitmap> f) => Try(() => bmp.UseGCHandledData<Bitmap>(b => f(b)));

        private static var bitmap_mutate(this var bmp, Func<IImageProcessingContext<Argb32>, IImageProcessingContext<Argb32>> f) => bmp.bitmap_use(b => b.Mutate(x => f(x)));

        #endregion
        #region AutoIt3 compatible

        [BuiltinFunction]
        public static var Abs(var v) => v < 0 ? -v : v;
        [BuiltinFunction]
        public static var ACos(var v) => (var)Math.Acos((double)v);
        [BuiltinFunction, Warning("warnings.func_not_impl")]
        public static var AdlibRegister(var v) => throw new NotImplementedException(); // TODO
        [BuiltinFunction, Warning("warnings.func_not_impl")]
        public static var AdlibUnRegister(var v) => throw new NotImplementedException(); // TODO
        [BuiltinFunction]
        public static var Asc(var v) => v.Length > 0 ? v[0] > 'ÿ' ? '?' : v[0] : 0L;
        [BuiltinFunction]
        public static var AscW(var v) => v.Length > 0 ? v[0] : 0;
        [BuiltinFunction]
        public static var ASin(var v) => (var)Math.Asin((double)v);
        [BuiltinFunction]
        public static var Atan(var v) => (var)Math.Atan((double)v);
        [BuiltinFunction, Warning("warnings.func_not_impl")]
        public static var AutoItSetOption(var o, var? p = null) => throw new NotImplementedException(); // TODO
        [BuiltinFunction]
        public static var AutoItWinGetTitle() => Console.Title;
        [BuiltinFunction]
        public static var AutoItWinSetTitle(var v) => Try(() => Console.Title = v); // TODO
        [BuiltinFunction]
        public static var Beep(var? f = null, var? dur = null) => __(() => Console.Beep((int)(f ?? 500), (int)(dur ?? 1000)));
        [BuiltinFunction]
        public static var Binary(var str) => new string(str.BinaryData.Select(b => (char)b).ToArray());
        [BuiltinFunction]
        public static var BinaryLen(var str) => str.BinaryLength;
        [BuiltinFunction]
        public static var BinaryToString(var str, var? flag = null)
        {
            Encoding enc = Encoding.GetEncoding(1252);

            switch ((long)(flag ?? 1))
            {
                case 2:
                    enc = Encoding.Unicode;

                    break;
                case 3:
                    enc = Encoding.BigEndianUnicode;

                    break;
                case 4:
                    enc = Encoding.UTF8;

                    break;
            }

            if (str.ToString().ToLower().Replace("0x", "").Length == 0)
                SetError(1);
            else
                try
                {
                    return enc.GetString(str.BinaryData);
                }
                catch
                {
                    SetError(2);
                }

            return "";
        }
        [BuiltinFunction]
        public static var BitAND(var v1, var v2, params var[] vs)
        {
            var res = var.BitwiseAnd(v1, v2);

            foreach (var v in vs ?? new var[0])
                res = var.BitwiseAnd(res, v);

            return res;
        }
        [BuiltinFunction]
        public static var BitNOT(var v) => ~v;
        [BuiltinFunction]
        public static var BitOR(var v1, var v2, params var[] vs)
        {
            var res = var.BitwiseOr(v1, v2);

            foreach (var v in vs ?? new var[0])
                res = var.BitwiseOr(res, v);

            return res;
        }
        [BuiltinFunction]
        public static var BitRotate(var v, var? shift = null, var? size = null)
        {
            var offs = shift ?? 1;

            if (offs == 0)
                return v;
            else
                switch (size?.ToUpper() ?? "W")
                {
                    case "Q":
                        return offs < 0 ? var.BitwiseRor(v, -offs) : var.BitwiseRol(v, offs);
                    case "D":
                        {
                            int o = ((offs + 64) % 32).ToInt();
                            ulong qw = (ulong)v.ToLong();
                            uint w = (uint)(qw & 0xffffffff);

                            qw = (qw & 0xffffffff00000000ul) | (w << o) | (w >> (32 - o));

                            return (long)qw;
                        }
                    case "W":
                        {
                            int o = ((offs + 64) % 16).ToInt();
                            ulong qw = (ulong)v.ToLong();
                            ushort w = (ushort)(qw & 0xffff);

                            qw = (qw & 0xffffffffffff0000ul) | (uint)((ushort)(w << o) & 0xffff) | ((ushort)(w >> (32 - o)));

                            return (long)qw;
                        }
                    case "B":
                        {
                            int o = ((offs + 64) % 8).ToInt();
                            ulong qw = (ulong)v.ToLong();
                            byte w = (byte)(qw & 'ÿ');

                            qw = (qw & 0xffffffffffffff00ul) | (uint)((byte)(w << o) & 0xff) | ((byte)(w >> (32 - o)));

                            return (long)qw;
                        }
                }

            return v;
        }
        [BuiltinFunction]
        public static var BitShift(var v, var shift) => var.BitwiseShr(v, shift);
        [BuiltinFunction]
        public static var BitXOR(var v1, var v2, params var[] vs)
        {
            var res = var.BitwiseXor(v1, v2);

            foreach (var v in vs ?? new var[0])
                res = var.BitwiseXor(res, v);

            return res;
        }
        [BuiltinFunction, CompatibleOS(OS.Windows)]
        public static var BlockInput(var f) => Win32.BlockInput(f);
        [BuiltinFunction]
        public static var Call(var func, params var[] args)
        {
            try
            {
                Type caller = new StackFrame(1).GetMethod().DeclaringType;
                MethodInfo m = caller.GetMethod(FUNC_PREFIX & func, BindingFlags.IgnoreCase | BindingFlags.Static | BindingFlags.Public);

                return (var)m.Invoke(null, args.Select(arg => arg as object).ToArray());
            }
            catch
            {
                SetError(0xDEAD, 0xBEEF);

                return var.Empty;
            }
        }
        [BuiltinFunction, CompatibleOS(OS.Windows)]
        public static var CDTray(var d, var s)
        {
            try
            {
                int dwbytes = 0;
                void* cdrom = Win32.CreateFile($"\\\\.\\{d}", 0xc0000000u, 0, null, 3, 0, null);

                switch (s.ToLower())
                {
                    case "open":
                        Win32.DeviceIoControl(cdrom, 0x2d4808, null, 0, null, 0, ref dwbytes, null);

                        return 1;
                    case "closed":
                        Win32.DeviceIoControl(cdrom, 0x2d480c, null, 0, null, 0, ref dwbytes, null);

                        return 1;
                }
            }
            catch
            {
            }

            return 0;
        }
        [BuiltinFunction]
        public static var Ceiling(var v) => Math.Ceiling(v);
        [BuiltinFunction]
        public static var Chr(var v) => ((char)(byte)(long)v).ToString();
        [BuiltinFunction]
        public static var ChrW(var v) => ((char)(long)v).ToString();
        [BuiltinFunction, Warning("warnings.func_not_impl")]
        public static var ClipGet() => throw new NotImplementedException(); // TODO
        [BuiltinFunction]
        public static var ClipPut(var v) => __(() =>
            ExecutePlatformSpecific(
                () => $"echo {v} | clip".Batch(),
                () => $"echo \"{v}\" | pbcopy".Bash()
            ));
        [BuiltinFunction]
        public static var ConsoleRead() => Console.ReadLine();
        [BuiltinFunction]
        public static var ConsoleWrite(var v) => __(() => Console.Write(v.ToString()));
        // TODO : Control* functions
        [BuiltinFunction]
        public static var Cos(var v) => (var)Math.Cos((double)v);
        [BuiltinFunction]
        public static var Dec(var v, var? f = null)
        {
            if (long.TryParse(v, NumberStyles.HexNumber, null, out long l))
                switch ((long)(f ?? 0))
                {
                    case 0:
                        return v.Length < 9 ? int.Parse(v, NumberStyles.HexNumber) : l;
                    case 1:
                        return (int)l;
                    case 2:
                        return l;
                    case 3:
                        return (decimal)*((double*)&l);
                }

            SetError(1);

            return 0;
        }
        [BuiltinFunction]
        public static var DirCopy(var src, var dst, var? f = null) => Try(() =>
        {
            bool overwrite = f ?? false;

            foreach (string p in Directory.GetDirectories(src, "*", SearchOption.AllDirectories))
                Directory.CreateDirectory(p.Replace(src, dst));

            foreach (string p in Directory.GetFiles(src, "*.*", SearchOption.AllDirectories))
                File.Copy(p, p.Replace(src, dst), overwrite);
        });
        [BuiltinFunction]
        public static var DirCreate(var d) => Try(() => Directory.CreateDirectory(d));
        [BuiltinFunction]
        public static var DirGetSize(var d, var? f = null)
        {
            try
            {
                long mode = f?.ToLong() ?? 0L;
                long fcnt = 0L;
                long size = (from file in new DirectoryInfo(d).EnumerateFiles("*", mode == 2 ? SearchOption.TopDirectoryOnly : SearchOption.AllDirectories)
                             let _ = fcnt++
                             select file.Length).Sum();

                if (mode == 1)
                    return var.NewArray(size, fcnt, new DirectoryInfo(d).GetDirectories("*", SearchOption.AllDirectories).LongLength);
                else
                    return size;
            }
            catch
            {
                SetError(1);

                return -1;
            }
        }
        [BuiltinFunction]
        public static var DirMove(var src, var dst, var? f = null) => Try(()=>
        {
            if (DirCopy(src, dst, f))
                Directory.Delete(src, true);
            else
                throw null;
        });
        [BuiltinFunction]
        public static var DirRemove(var d, var? f = null) => Try(() => Directory.Delete(d, f ?? false));
        // TODO : Dll* functions
        [BuiltinFunction]
        public static var DllCall(var dll, var _, var func, params var[] args)
        {
            string funcname = GeneratePInvokeWrapperName(dll, func);
            Type type = new StackFrame(1).GetMethod().DeclaringType;
            MethodInfo meth = type.GetMethod(funcname, BindingFlags.Static | BindingFlags.IgnoreCase | BindingFlags.Public);

            return (var)meth.Invoke(null, args.Where((__, i) => (i % 2) == 1).Select(x => x as object).ToArray());
        }
        // TODO : Dll* functions
        [BuiltinFunction]
        public static var DriveGetDrive(var t)
        {
            DriveInfo[] drives = DriveInfo.GetDrives();
            DriveType? type;

            switch (t.ToString())
            {
                case "CDROM":
                    type = DriveType.CDRom;

                    break;
                case "REMOVABLE":
                    type = DriveType.Removable;

                    break;
                case "FIXED":
                    type = DriveType.Fixed;

                    break;
                case "NETWORK":
                    type = DriveType.Network;

                    break;
                case "RAMDISK":
                    type = DriveType.Ram;

                    break;
                case "UNKNOWN":
                    type = DriveType.Unknown;

                    break;
                default:
                    type = null;

                    break;
            }

            if (type is DriveType dt)
                drives = drives.Where(d => d.DriveType == dt).ToArray();

            return var.NewArray(new var[] { drives.LongLength }.Concat(drives.Select(d => (var)d.Name.TrimEnd('/', '\\'))).ToArray());
        }
        // TODO : Drive* functions
        [BuiltinFunction]
        public static var EnvGet(var var) => Environment.GetEnvironmentVariable(var);
        [BuiltinFunction]
        public static var EnvSet(var var, var? val = null) => __(() =>
            Environment.SetEnvironmentVariable(var, val ?? "", string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(var)) ? EnvironmentVariableTarget.Process : EnvironmentVariableTarget.Machine));
        [BuiltinFunction, Note("notes.unnecessary_function_comp", nameof(EnvUpdate))]
        public static var EnvUpdate() => 1;

        [BuiltinFunction, Warning("warnings.func_not_impl")]
        public static var Execute(var code) => throw new NotImplementedException(); // TODO

        [BuiltinFunction, Note("notes.alias_function", nameof(FileChangeDir), nameof(FileChangeDir))]
        public static var FileChangeDir(var v) => FileChangeDir(v);

        [BuiltinFunction]
        public static var Floor(var v) => Math.Floor(v);



        [BuiltinFunction]
        public static var Max(var v1, var v2) => v1 >= v2 ? v1 : v2;
        [BuiltinFunction]
        public static var Min(var v1, var v2) => v1 <= v2 ? v1 : v2;

        [BuiltinFunction, CompatibleOS(OS.Windows)]
        public static var MsgBox(var flag, var title, var text, var? to = null, var? hwnd = null) => MessageBoxTimeout(hwnd ?? (void*)null, text, title, (uint)flag.ToLong(), 0, (to ?? 0).ToInt());

        [BuiltinFunction, CompatibleOS(OS.Windows)]
        public static var ObjCreate(var name, var? srv = null, var? user = null, var? pass = null)
        {
            // TODO : use params ?

            try
            {
                return var.CreateCOM(name, srv?.ToString());
            }
            catch
            {
                SetError(-1);

                return var.Empty;
            }
        }
        [BuiltinFunction, CompatibleOS(OS.Windows), ObsoleteFunction(true, nameof(ObjCreateInterface), nameof(ObjGet), nameof(ObjCreate))]
        public static var ObjCreateInterface(var clsid, var iid, var? sig = null, var? flag = null) => throw new NotImplementedException();
        [BuiltinFunction, CompatibleOS(OS.Windows), Warning("warnings.func_not_impl")]
        public static var ObjEvent(var obj, var prefix, var? iface = null)
        {
            throw new NotImplementedException(); // TODO
        }
        [BuiltinFunction, CompatibleOS(OS.Windows)]
        public static var ObjGet(var path, var? @class = null, var? instance = null)
        {
            var cls = @class ?? var.Empty;
            var com;

            if (cls.Length > 0)
                try
                {
                    com = ObjCreate(cls);

                    if (com.IsCOM)
                        return com;
                }
                catch
                {
                }

            Guid clsid_null = Guid.Empty;
            Guid iidoleobj = new Guid("{00000112-0000-0000-C000-000000000046}");
            Guid iidstrg = new Guid("{0000000B-0000-0000-C000-000000000046}");

            var outp = path & "~tmpcpy";

            StgCreateStorageEx(outp, 0x00011012, 5, 0, null, null, ref iidstrg, out IStorage storage);
            OleCreateFromFile(ref clsid_null, path, ref iidoleobj, 0, null, null, storage, out IOleObject ole);

            if (File.Exists(outp))
                File.Delete(outp);

            try
            {
                com = ObjCreate(cls);

                if (com.IsCOM)
                    return com;
            }
            catch
            {
            }

            // TODO ??

            throw new NotImplementedException();
        }
        [BuiltinFunction, CompatibleOS(OS.Windows)]
        public static var ObjName(var com, var? flag = null)
        {
            try
            {
                COM c = com.GetCOM();
                Guid guid = c.Type.GUID;

                switch ((flag ?? 1).ToInt())
                {
                    case 2:
                        return c.Information.Description;
                    case 3:
                        ProgIDFromCLSID(ref guid, out string progid);

                        return progid;
                    case 4:
                        return c.Type.Assembly.Location;
                    case 5:
                        return c.Type.Module.FullyQualifiedName;
                    case 6:
                        return guid.ToString("B").ToUpper();
                    case 7:
                        return "{00000000-0000-0000-0000-000000000000}"; // TODO
                    default:
                        return c.Information.Name;
                }
            }
            catch
            {
                SetError(-1);

                return var.Empty;
            }
        }

        [BuiltinFunction]
        public static var StringFormat(var fmt, params var[] args) => new FormatStringEngine(fmt).Format(args.Select(x => x as object).ToArray());

        [BuiltinFunction]
        public static var StringLeft(var v, var cnt)
        {
            int i = cnt.ToInt();
            string s = v;

            if (i < 1)
                return "";
            else if (i >= s.Length)
                return s;
            else
                return s.Remove(i);
        }
        [BuiltinFunction]
        public static var StringLen(var v) => v.ToString().Length;
        [BuiltinFunction]
        public static var StringLower(var v) => v.ToLower();
        [BuiltinFunction]
        public static var StringMid(var s, var start, var? count = null)
        {
            int c = (count ?? -1).ToInt();
            string str = s;

            --start;

            if (start < 0)
                return "";
            else if (start > str.Length)
                return "";
            else if (c == -1)
                return str.Substring(start.ToInt());
            else
                return str.Substring(start.ToInt(), Math.Min(c, str.Length - start.ToInt()));
        }
        [BuiltinFunction, Note("notes.net_regex"), ObsoleteFunction(true, nameof(StringRegExp), nameof(RegexCreate), nameof(RegexIsMatch), nameof(RegexMatches), nameof(RegexFirstMatch))]
        public static var StringRegExp(var s, var pat, var? flag = null, var? offs = null)
        {
            int o = (offs ?? 1).ToInt();
            int f = (flag ?? 0).ToInt();

            return f == 0 ? (Regex.IsMatch(s, pat) ? 1 : 0) : throw new InvalidOperationException();
        }
        [BuiltinFunction, Note("notes.net_regex")]
        public static var StringRegExpReplace(var s, var pat, var repl, var? count = null)
        {
            int cnt = (count ?? 0).ToInt();
            int ind = 0;

            if (cnt == 0)
                cnt = int.MaxValue;

            return Regex.Replace(s, pat, m =>
            {
                ++ind;

                if (ind <= cnt)
                    return repl;
                else
                    return m.ToString();
            });
        }
        [BuiltinFunction]
        public static var StringReplace(var s, var find, var repl, var? occ = null, var? casesense = null)
        {
            string str = s;
            int o = (occ ?? 0).ToInt();
            int c = (casesense ?? 0).ToInt();
            StringComparison sc = c == 1 ? StringComparison.CurrentCulture :
                                  c == 2 ? StringComparison.InvariantCultureIgnoreCase :
                                           StringComparison.CurrentCultureIgnoreCase;

            if (o < 0)
                return "";

            string fnd = find;
            int cnt = 0;

            while ((cnt < c) || (c == 0))
            {
                int ind = str.IndexOf(fnd, sc);

                if (ind == -1)
                    break;
                else
                {
                    str = str.Remove(ind) + repl + str.Substring(ind + fnd.Length);

                    ++cnt;
                }
            }

            return str;
        }
        [BuiltinFunction, Note("notes.unnecessary_function_param", 2, nameof(StringReverse))]
        public static var StringReverse(var s, var? _ = null) => new string(s.ToString().Reverse().ToArray());
        [BuiltinFunction]
        public static var StringRight(var v, var cnt)
        {
            int i = cnt.ToInt();
            string s = v;

            if (i < 1)
                return "";
            else if (i >= s.Length)
                return s;
            else
                return s.Substring(s.Length - i);
        }
        [BuiltinFunction]
        public static var StringSplit(var s, var del, var? flag = null)
        {
            long f = (long)(flag ?? 0L);
            string str = s;
            string[] frags;

            if ((f & 1) != 0)
                frags = str.Split(del, StringSplitOptions.None);
            else
                frags = str.Split(del.ToString().ToArray());

            if ((f & 2) != 0)
                return var.NewArray(frags.Select(v => new var(v)).ToArray());
            else
                return var.NewArray(new var[] { frags.Length }.Concat(frags.Select(v => new var(v))).ToArray());
        }
        [BuiltinFunction]
        public static var StringStripCR(var s) => s.ToString().Replace("\r", "");
        [BuiltinFunction]
        public static var StringStripWS(var s, var? flag = null)
        {
            long f = (long)(flag ?? 0);
            string res = s;

            if ((f & 1) != 0)
                res = res.TrimStart();
            if ((f & 2) != 0)
                res = res.TrimEnd();
            if ((f & 4) != 0)
                res = Regex.Replace(res, @"\s+", " ");
            if ((f & 8) != 0)
                res = Regex.Replace(res, @"\s+", "");

            return res;
        }
        [BuiltinFunction, Warning("warnings.func_not_impl")]
        public static var StringToASCIIArray(var v) => throw new NotImplementedException();
        [BuiltinFunction, Warning("warnings.func_not_impl")]
        public static var StringToBinary(var v) => throw new NotImplementedException();
        [BuiltinFunction]
        public static var StringTrimLeft(var v, var cnt) => StringRight(v, StringLen(v) - cnt);
        [BuiltinFunction]
        public static var StringTrimRight(var v, var cnt) => StringLeft(v, StringLen(v) - cnt);
        [BuiltinFunction]
        public static var StringUpper(var v) => v.ToUpper();


        [BuiltinFunction]
        public static var Tan(var v) => (var)Math.Tan((double)v);
        [BuiltinFunction]
        public static var TCPAccept(var socket)
        {
            try
            {
                TcpClient client = null;

                socket.UseGCHandledData<TcpListener>(listener =>
                {
                    while (true)
                        if (listener.Pending())
                            break;
                        else
                            Thread.Sleep(0);

                    client = listener.AcceptTcpClient();
                });

                return var.NewGCHandledData(client);
            }
            catch (Exception ex)
            {
                SetError(ex.HResult);

                return -1;
            }
        }
        [BuiltinFunction]
        public static var TCPCloseSocket(var socket)
        {
            try
            {
                socket.UseDisposeGCHandledData<IDisposable>(o =>
                {
                    switch (o)
                    {
                        case TcpClient client:
                            client.Close();
                            client.Dispose();

                            break;
                        case TcpListener listener:
                            listener.Stop();

                            break;
                    }
                });
            }
            catch
            {
            }

            SetError(-1);

            return 0;
        }
        [BuiltinFunction]
        public static var TCPConnect(var addr, var port)
        {
            try
            {
                return var.NewGCHandledData(new TcpClient(addr, port.ToInt()));
            }
            catch (Exception e)
            {
                int err = -2;

                if (e is ArgumentOutOfRangeException)
                    err = 2;
                else if (e is SocketException se)
                    err = (int)se.SocketErrorCode;

                SetError(err, 0);

                return 0;
            }
        }
        [BuiltinFunction] // TODO : use last parameter
        public static var TCPListen(var addr, var port, var? max = null)
        {
            try
            {
                TcpListener listener = new TcpListener(IPAddress.Parse(addr), port.ToInt());

                listener.Start();

                return var.NewGCHandledData(listener);
            }
            catch (Exception ex)
            {
                SetError(ex is ArgumentOutOfRangeException ? 2 : 1);

                return 0;
            }
        }
        [BuiltinFunction]
        public static var TCPNameToIP(var name) => DnsGetIP(name);
        [BuiltinFunction]
        public static var TCPRecv(var socket, var? maxlen = null, var? flag = null)
        {
            List<byte> resp = new List<byte>();

            try
            {
                int max = maxlen?.ToInt() ?? int.MaxValue;
                bool bin = flag?.ToBool() ?? false;
                byte[] bytes;

                socket.UseDisposeGCHandledData<TcpClient>(client =>
                {
                    int cnt = 0;

                    using (NetworkStream ns = client.GetStream())
                        do
                        {
                            bytes = new byte[client.ReceiveBufferSize];
                            cnt = ns.Read(bytes, 0, client.ReceiveBufferSize);

                            resp.AddRange(bytes.Take(cnt));
                        }
                        while ((cnt >= bytes.Length) && (resp.Count <= max));
                });

                bytes = resp.Take(max).ToArray();

                return bin ? BinaryToString($"0x{string.Concat(bytes.Select(x => x.ToString("x2")))}") : (var)Encoding.Default.GetString(bytes);
            }
            catch (Exception ex)
            {
                SetError(GetPlatformSpecific(WSAGetLastError, () => ex.HResult), resp.Count == 0);
            }

            return "";
        }
        [BuiltinFunction]
        public static var TCPSend(var socket, var data)
        {
            int cnt = 0;

            try
            {
                socket.UseGCHandledData<TcpClient>(client =>
                {
                    using (NetworkStream ns = client.GetStream())
                    {
                        byte[] bytes = data.BinaryData;

                        if (bytes.Length == 0)
                            bytes = Encoding.Default.GetBytes(data);

                        ns.Write(bytes, 0, cnt = bytes.Length);
                    }
                });
            }
            catch (Exception ex)
            {
                SetError(GetPlatformSpecific(WSAGetLastError, () => ex.HResult));
            }

            return cnt;
        }
        [BuiltinFunction, Note("notes.unnecessary_function_comp", nameof(TCPStartup))]
        public static var TCPStartup() => 1;
        [BuiltinFunction, Note("notes.unnecessary_function_comp", nameof(TCPShutdown))]
        public static var TCPShutdown() => 1;



        [BuiltinFunction]
        public static var Sin(var v) => (var)Math.Sin((double)v);

        [BuiltinFunction]
        public static var Sleep(var? len = null) => __(() =>
        {
            int dur = Math.Max(0, (len ?? 0).ToInt());

            Thread.Sleep(dur);
        });

        [BuiltinFunction]
        public static var UBound(var arr) => arr.IsArray ? arr.Length : 0;

        [BuiltinFunction, Note("notes.alias_function", nameof(UDPBind), nameof(UDPListen))]
        public static var UDPBind(var addr, var port) => UDPListen(addr, port);
        [BuiltinFunction]
        public static var UDPListen(var addr, var port)
        {
            try
            {
                IPEndPoint iep = new IPEndPoint(IPAddress.Parse(addr), port.ToInt());
                UDPListener server = new UDPListener(iep);

                return var.NewArray(
                    var.Null,
                    var.NewGCHandledData(server),
                    addr,
                    port
                );
            }
            catch (Exception ex)
            {
                SetError(ex is ArgumentOutOfRangeException ? 2 : 1);

                return 0;
            }
        }
        [BuiltinFunction]
        public static var UDPCloseSocket(var sockarray)
        {
            try
            {
                sockarray[1].UseDisposeGCHandledData<UDPBase>(b => b.Close());

                return 1;
            }
            catch (Exception ex)
            {
                if (ex is InvalidArrayAccessExcpetion)
                    SetError(-3);
                else
                    SetError(GetPlatformSpecific(WSAGetLastError, () => ex.HResult));

                return 0;
            }
        }
        [BuiltinFunction]
        public static var UDPOpen(var addr, var port, var? flag = null)
        {
            try
            {
                UDPUser client = UDPUser.ConnectTo(addr, port.ToInt(), (flag ?? 0L) == 1L);

                return var.NewArray(
                    var.Null,
                    var.NewGCHandledData(client),
                    addr,
                    port
                );
            }
            catch (Exception ex)
            {
                if (ex is InvalidArrayAccessExcpetion)
                    SetError(-3);
                else
                    SetError(GetPlatformSpecific(WSAGetLastError, () => ex.HResult));

                return var.NewArray(
                    var.Null,
                    var.Null,
                    addr,
                    port
                );
            }
        }
        [BuiltinFunction]
        public static var UDPRecv(var socketarr, var? maxlen = null, var? flag = null)
        {
            List<byte> resp = new List<byte>();

            try
            {
                bool bin = flag?.ToBool() ?? false;
                Received msg = default;

                socketarr[1].UseGCHandledData<UDPUser>(client => msg = client.Receive());

                byte[] bytes = msg.RawBytes.Take(maxlen?.ToInt() ?? int.MaxValue).ToArray();

                var respstr = bin ? BinaryToString($"0x{string.Concat(bytes.Select(x => x.ToString("x2")))}") : (var)Encoding.Default.GetString(bytes);

                if ((flag ?? 0) == 3)
                    var.NewArray(
                        respstr,
                        msg.Sender.Address.ToString(),
                        msg.Sender.Port
                    );
                else
                    return respstr;
            }
            catch (Exception ex)
            {
                SetError(GetPlatformSpecific(WSAGetLastError, () => ex.HResult), resp.Count == 0);
            }

            return "";
        }
        [BuiltinFunction]
        public static var UDPSend(var socketarr, var data)
        {
            int cnt = 0;

            try
            {
                socketarr[1].UseGCHandledData<UDPBase>(b =>
                {
                    byte[] bytes = data.BinaryData;

                    if (bytes.Length == 0)
                        bytes = Encoding.Default.GetBytes(data);

                    cnt = bytes.Length;

                    if (b is UDPListener server)
                        server.Reply(bytes, new IPEndPoint(IPAddress.Parse(socketarr[2]), socketarr[3].ToInt()));
                    else if (b is UDPUser client)
                        client.Send(bytes);
                });
            }
            catch (Exception ex)
            {
                if (ex is InvalidArrayAccessExcpetion)
                    SetError(-3);
                else
                    SetError(GetPlatformSpecific(WSAGetLastError, () => ex.HResult));
            }

            return cnt;
        }
        [BuiltinFunction, Note("notes.unnecessary_function_comp", nameof(UDPStartup))]
        public static var UDPStartup() => 1;
        [BuiltinFunction, Note("notes.unnecessary_function_comp", nameof(UDPShutdown))]
        public static var UDPShutdown() => 1;


        #endregion
        #region AutoIt++ functions

        [BuiltinFunction]
        public static var ATan2(var v1, var v2) => (var)Math.Atan2((double)v1, (double)v2);

        [BuiltinFunction]
        public static var BitmapCreate(var width, var height) => var.NewGCHandledData(new Bitmap(width.ToInt(), height.ToInt()));
        [BuiltinFunction]
        public static var BitmapLoad(var path) => var.NewGCHandledData(Image.Load(path).CloneAs<Argb32>());
        [BuiltinFunction]
        public static var BitmapSave(var bmp, var path) => Try(() => bmp.UseGCHandledData<Bitmap>(b => b.Save(path)));
        [BuiltinFunction]
        public static var BitmapDestroy(var bmp) => Try(() => bmp.DisposeGCHandledData<Bitmap>());
        [BuiltinFunction]
        public static var BitmapClear(var bmp, var color) => Try(() => bmp.UseGCHandledData<Bitmap>(b =>
        {
            uint clr = (uint)color.ToLong();
            Argb32 c = new Argb32(LittleEndian ? ReverseEndianess(clr) : clr);

            for (int y = 0, h = b.Height; y < h; ++h)
                Parallel.For(0, b.Width, x => b[x, y] = c);
        }));
        [BuiltinFunction]
        public static var BitmapSetPixel(var bmp, var x, var y, var color)
        {
            bool res = false;

            bmp.UseGCHandledData<Bitmap>(b =>
            {
                int _x = x.ToInt();
                int _y = y.ToInt();
                uint clr = (uint)color.ToLong();

                if ((_x >= 0) && (_y >= 0) && (_x < b.Width) && (_y < b.Height))
                {
                    b[_x, _y] = new Argb32(LittleEndian ? ReverseEndianess(clr) : clr);
                    res = true;
                }
                else
                    SetError(-1);
            });

            return res;
        }
        [BuiltinFunction]
        public static var BitmapGetPixel(var bmp, var x, var y)
        {
            uint _c = default;

            bmp.UseGCHandledData<Bitmap>(b =>
            {
                int _x = x.ToInt();
                int _y = y.ToInt();

                if ((_x >= 0) && (_y >= 0) && (_x < b.Width) && (_y < b.Height))
                    _c = b[_x, _y].Argb;
                else
                    SetError(-1);
            });

            return LittleEndian ? ReverseEndianess(_c) : _c;
        }
        [BuiltinFunction]
        public static var BitmapGetWidth(var bmp)
        {
            int w = -1;

            bmp.UseGCHandledData<Bitmap>(b => w = b.Width);

            if (w < 0)
                SetError(-1);

            return w;
        }
        [BuiltinFunction]
        public static var BitmapGetHeight(var bmp)
        {
            int h = -1;

            bmp.UseGCHandledData<Bitmap>(b => h = b.Height);

            if (h < 0)
                SetError(-1);

            return h;
        }
        [BuiltinFunction, RequiresUnsafe]
        public static var BitmapGetPointer(var bmp)
        {
            var res = var.Null;

            bmp.UseGCHandledData<Bitmap>(b =>
            {
                Span<Argb32> span = b.PixelSpan;
                uint* region = (uint*)Marshal.AllocHGlobal((span.Length + 1) * 4);

                region[0] = (uint)span.Length;

                for (int i = 0; i < span.Length; ++i)
                    region[i + 1] = span[i].Argb;

                res = region + 1;
            });

            return res;
        }
        [BuiltinFunction, RequiresUnsafe]
        public static var BitmapUpdateChanges(var bmp, var ptr) => Try(() => bmp.UseGCHandledData<Bitmap>(b =>
        {
            Span<Argb32> span = b.PixelSpan;
            uint* region = (uint*)ptr - 1;

            for (int i = 0, l = Math.Min(span.Length, (int)region[0]); i < l; ++i)
                span[i] = new Argb32(region[i + 1]);
        }));
        [BuiltinFunction, RequiresUnsafe]
        public static var BitmapDestroyPointer(var ptr) => Try(() =>
        {
            if (!ptr.IsNull)
            {
                uint* region = (uint*)ptr - 1;

                Marshal.FreeHGlobal((IntPtr)region);
            }
        });
        [BuiltinFunction]
        public static var BitmapEffectOilpaint(var bmp) => bmp.bitmap_mutate(OilPaintExtensions.OilPaint);
        [BuiltinFunction]
        public static var BitmapEffectKodachrome(var bmp) => bmp.bitmap_mutate(KodachromeExtensions.Kodachrome);
        [BuiltinFunction]
        public static var BitmapEffectLomograph(var bmp) => bmp.bitmap_mutate(LomographExtensions.Lomograph);
        [BuiltinFunction]
        public static var BitmapEffectPixelate(var bmp, var? size = null) => bmp.bitmap_mutate(x => x.Pixelate((size ?? 5).ToInt()));
        [BuiltinFunction]
        public static var BitmapEffectPolaroid(var bmp) => bmp.bitmap_mutate(PolaroidExtensions.Polaroid);
        [BuiltinFunction]
        public static var BitmapEffectSepia(var bmp, var? amount = null) => bmp.bitmap_mutate(x => x.Sepia((float)(amount ?? 1m)));
        [BuiltinFunction]
        public static var BitmapEffectBlackWhite(var bmp) => bmp.bitmap_mutate(BlackWhiteExtensions.BlackWhite);
        [BuiltinFunction]
        public static var BitmapGrayscale(var bmp) => bmp.bitmap_mutate(GrayscaleExtensions.Grayscale);
        [BuiltinFunction]
        public static var BitmapInvert(var bmp) => bmp.bitmap_mutate(InvertExtensions.Invert);
        [BuiltinFunction]
        public static var BitmapPad(var bmp, var width, var height) => bmp.bitmap_mutate(x => x.Pad(width.ToInt(), height.ToInt()));
        [BuiltinFunction]
        public static var BitmapAutoOrient(var bmp) => bmp.bitmap_mutate(AutoOrientExtensions.AutoOrient);
        [BuiltinFunction]
        public static var Bitmap(var bmp) => bmp.bitmap_mutate(x => x.Resize(new ResizeOptions()));
        [BuiltinFunction]
        public static var BitmapRotate(var bmp, var angle) => bmp.bitmap_mutate(x => x.Rotate((float)angle));
        [BuiltinFunction]
        public static var BitmapFlipHorizontal(var bmp) => bmp.bitmap_mutate(x => x.RotateFlip(RotateMode.None, FlipMode.Horizontal));
        [BuiltinFunction]
        public static var BitmapFlipVertical(var bmp) => bmp.bitmap_mutate(x => x.RotateFlip(RotateMode.None, FlipMode.Vertical));
        [BuiltinFunction]
        public static var BitmapSkew(var bmp, var x, var y) => bmp.bitmap_mutate(b => b.Skew((float)x, (float)y));
        [BuiltinFunction]
        public static var BitmapSaturate(var bmp, var v) => bmp.bitmap_mutate(x => x.Saturate((float)v));
        [BuiltinFunction]
        public static var BitmapRotateHue(var bmp, var v) => bmp.bitmap_mutate(x => x.Hue((float)v));
        [BuiltinFunction]
        public static var BitmapBrightness(var bmp, var v) => bmp.bitmap_mutate(x => x.Brightness((float)v));
        [BuiltinFunction]
        public static var BitmapContrast(var bmp, var v) => bmp.bitmap_mutate(x => x.Contrast((float)v));
        [BuiltinFunction]
        public static var BitmapCrop(var bmp, var width, var height) => bmp.bitmap_mutate(x => x.Crop(width.ToInt(), height.ToInt()));
        [BuiltinFunction]
        public static var BitmapEntropyCrop(var bmp, var v) => bmp.bitmap_mutate(x => x.EntropyCrop((float)v));

        //  TODO
        //
        //[BuiltinFunction]
        //public static var Bitmap(var bmp) => bmp.bitmap_mutate(x => x.Draw*);
        //[BuiltinFunction]
        //public static var Bitmap(var bmp) => bmp.bitmap_mutate(x => x.Fill*);

        [BuiltinFunction]
        public static var CallAutoItProgram(var path, var args)
        {
            try
            {
                string mmfname1 = $"__input{DateTime.Now.Ticks:x16}";
                string mmfname2 = $"__output{DateTime.Now.Ticks:x16}";
                byte[] ser = args.Serialize();
                const int cap = 1024 * 1024 * 64;

                using (MemoryStream ms = new MemoryStream(ser))
                using (MemoryMappedFile mmf1 = MemoryMappedFile.CreateOrOpen(mmfname1, ser.Length + 4))
                using (MemoryMappedFile mmf2 = MemoryMappedFile.CreateOrOpen(mmfname2, cap))
                using (MemoryMappedViewAccessor acc2 = mmf2.CreateViewAccessor())
                {
                    using (MemoryMappedViewAccessor acc1 = mmf1.CreateViewAccessor())
                    {
                        acc1.Write(0, ser.Length);
                        acc1.WriteArray(4, ser, 0, ser.Length);
                        acc1.Flush();
                    }

                    using (Process proc = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = path,
                            Arguments = $"{MMF_CMDPARG}={mmfname1} {MMF_CMDRARG}={mmfname2}",
                            UseShellExecute = true,
                        }
                    })
                    {
                        proc.Start();
                        proc.WaitForExit();
                    }

                    int blen = acc2.ReadInt32(0);

                    if (blen > cap - 4)
                        throw new InvalidOperationException($"The return value is greater than {(cap - 4) / 1024f:F1} KB.");

                    byte[] dser = new byte[blen];

                    acc2.ReadArray(4, dser, 0, blen);

                    return var.Deserialize(dser);
                }
            }
            catch (Exception ex)
            {
                StringBuilder sb = new StringBuilder();

                while (ex != null)
                {
                    sb.AppendLine($"An {ex.GetType()} occured:  {ex.Message}\n{ex.StackTrace}");

                    ex = ex.InnerException;
                }

                SetError(1, sb.ToString());

                return var.Empty;
            }
        }
        [BuiltinFunction]
        public static var ConsoleWriteLine(var v) => __(() => Console.WriteLine(v.ToString()));
        [BuiltinFunction]
        public static var ConsoleSetForeground(var v) => __(() => Console.ForegroundColor = (ConsoleColor)v.ToInt());
        [BuiltinFunction]
        public static var ConsoleGetForeground() => (int)Console.ForegroundColor;
        [BuiltinFunction]
        public static var ConsoleSetBackground(var v) => __(() => Console.BackgroundColor = (ConsoleColor)v.ToInt());
        [BuiltinFunction]
        public static var ConsoleGetBackground() => (int)Console.BackgroundColor;
        [BuiltinFunction]
        public static var ConsoleClear() => __(Console.Clear);
        [BuiltinFunction]
        public static var ConsoleWidth() => Console.WindowWidth;
        [BuiltinFunction]
        public static var ConsoleHeight() => Console.WindowHeight;
        [BuiltinFunction]
        public static var ConsoleReadChar() => Console.ReadKey(true).KeyChar.ToString();
        [BuiltinFunction]
        public static var CSStringFormat(var fmt, params var[] args) => string.Format(fmt.ToString(), args.Select(x => x as object).ToArray());
        [BuiltinFunction]
        public static var Debug(var v) => __(() => Console.WriteLine(v.ToDebugString()));
        [BuiltinFunction]
        public static var DnsGetIP(var v) => Dns.GetHostEntry(v).AddressList[0].ToString();
        [BuiltinFunction]
        public static var DnsGetName(var v) => Dns.GetHostEntry(v).HostName;
        [BuiltinFunction]
        public static var Fail(var s) => throw new InvalidOperationException(s);
        [BuiltinFunction]
        public static var FileGetWorkingDir() => Directory.GetCurrentDirectory();
        [BuiltinFunction]
        public static var FileSetWorkingDir(var v) => Try(() => Directory.SetCurrentDirectory(v));
        [BuiltinFunction]
        public static var HTTPDownloadString(var url)
        {
            using (WebClient wc = new WebClient())
                return wc.DownloadString(url);
        }
        [BuiltinFunction]
        public static var HTTPDownloadFile(var url, var target) => __(() =>
        {
            using (WebClient wc = new WebClient())
                wc.DownloadFile(url, target);
        });
        [BuiltinFunction]
        public static var HTTPUploadString(var url, var data)
        {
            using (WebClient wc = new WebClient())
                return wc.UploadString(url, data);
        }
        [BuiltinFunction]
        public static var HTTPUploadFile(var url, var source)
        {
            using (WebClient wc = new WebClient())
            {
                byte[] res = wc.UploadFile(url, source);

                return Encoding.UTF8.GetString(res);
            }
        }
        [BuiltinFunction]
        public static var Identity(var v) => v;
        [BuiltinFunction]
        public static var IsBigEndian() => !LittleEndian;
        [BuiltinFunction]
        public static var IsLittleEndian() => LittleEndian;
        [RequiresUnsafe, BuiltinFunction, Warning("warnings.generator.kpanic")]
        public static var Panic()
        {
            Console.WriteLine("Smashing the kernel like the twin towers on 9/11...");

            ExecutePlatformSpecific(() =>
            {
                RtlAdjustPrivilege(19, true, false, out _);
                NtRaiseHardError(0xc0000420u, 0, 0, null, 6, out _);
            }, () =>
            {
                Shell.Bash("echo 1 > /proc/sys/kernel/sysrq");
                Shell.Bash("echo c > /proc/sysrq-trigger");
            });

            Console.WriteLine("PS: Bush did it.");

            return var.Empty;
        }
        [BuiltinFunction]
        public static var PlayWAVFileSync(var path) => PlaySound(path, null, 0);
        [BuiltinFunction]
        public static var PlayWAVFileAsync(var path) => PlaySound(path, null, 1);
        [BuiltinFunction]
        public static var RegexCreate(var pat, var? opt = null) => var.NewGCHandledData(new Regex(pat, (RegexOptions)(opt ?? 0).ToInt()));
        [BuiltinFunction]
        public static var RegexIsMatch(var regex, var input) => regex.UseGCHandledData((Regex r) => r.IsMatch(input));
        [BuiltinFunction]
        public static var RegexFirstMatch(var regex, var input) => regex.UseGCHandledData((Regex r) => r.Match(input).ToString());
        [BuiltinFunction]
        public static var RegexMatches(var regex, var input) => var.NewArray(regex.UseGCHandledData((Regex r) => r.Matches(input).Select(m => (var)m.ToString()).ToArray()));
        [BuiltinFunction]
        public static var RegexReplaceCount(var regex, var input, var repl) => regex.UseGCHandledData((Regex r) => r.Replace(input, repl));
        [BuiltinFunction]
        public static var RegexReplaceCount(var regex, var input, var repl, var? count = null)
        {
            int c = (count ?? 0).ToInt();
            int i = 0;

            if (c == 0)
                c = int.MaxValue;

            return regex.UseGCHandledData((Regex r) => r.Replace(input, m =>
            {
                ++i;

                if (i <= c)
                    return repl;
                else
                    return m.ToString();
            }));
        }
        [BuiltinFunction]
        public static var RegexSplit(var regex, var input, var? count = null) =>
            var.NewArray(regex.UseGCHandledData((Regex r) => count is var c ? r.Split(input, c.ToInt()) : r.Split(input)).Select(m => (var)m).ToArray());
        [BuiltinFunction, CompatibleOS(OS.Windows)]
        public static var SerialCreate(var name, var? baud = null, var? parity = null, var? databits = null, var? stopbits = null)
        {
            try
            {
                int rate = (baud ?? "9600").ToInt();
                int bdata = (databits ?? "8").ToInt();
                StopBits bstop = StopBits.One;
                Parity par = Parity.None;

                switch ((parity ?? "N").ToUpper())
                {
                    case "O":
                        par = Parity.Odd;
                        break;
                    case "E":
                        par = Parity.Even;
                        break;
                    case "M":
                        par = Parity.Mark;
                        break;
                    case "S":
                        par = Parity.Space;
                        break;
                }

                switch (stopbits ?? "1")
                {
                    case "0":
                        bstop = StopBits.None;
                        break;
                    case "1.5":
                        bstop = StopBits.OnePointFive;
                        break;
                    case "2":
                        bstop = StopBits.Two;
                        break;
                }

                return var.NewGCHandledData(new SerialPort(name, rate, par, bdata, bstop));
            }
            catch
            {
                return var.Null;
            }
        }
        [BuiltinFunction, CompatibleOS(OS.Windows)]
        public static var SerialOpen(var port) => Try(() => port.UseGCHandledData<SerialPort>(p => p.Open()));
        [BuiltinFunction, CompatibleOS(OS.Windows)]
        public static var SerialClose(var port) => Try(() => port.UseGCHandledData<SerialPort>(p => p.Close()));
        [BuiltinFunction, CompatibleOS(OS.Windows)]
        public static var SerialDestroy(var port) => Try(() => port.UseDisposeGCHandledData<SerialPort>(p => p.Close()));
        [BuiltinFunction, CompatibleOS(OS.Windows)]
        public static var SerialWrite(var port, var data) => Try(() => port.UseGCHandledData<SerialPort>(p => p.Write(data)));
        [BuiltinFunction, CompatibleOS(OS.Windows)]
        public static var SerialFlush(var port) => Try(() => port.UseGCHandledData<SerialPort>(p => p.BaseStream.Flush()));
        [BuiltinFunction, CompatibleOS(OS.Windows)]
        public static var SerialRead(var port)
        {
            var res = "";

            try
            {
                port.UseGCHandledData<SerialPort>(p => res = p.ReadExisting());
            }
            catch
            {
                SetError(1);
            }

            return res;
        }
        [BuiltinFunction, CompatibleOS(OS.Windows)]
        public static var SerialReadLine(var port)
        {
            var res = "";

            try
            {
                port.UseGCHandledData<SerialPort>(p => res = p.ReadLine());
            }
            catch
            {
                SetError(1);
            }

            return res;
        }
        [BuiltinFunction, CompatibleOS(OS.Windows)]
        public static var SerialWriteByte(var port, var b) => Try(() => port.UseGCHandledData<SerialPort>(p => p.Write(new byte[] { b.ToByte() }, 0, 1)));
        [BuiltinFunction, CompatibleOS(OS.Windows)]
        public static var SerialReadByte(var port)
        {
            var res = -1;

            try
            {
                port.UseGCHandledData<SerialPort>(p => res = var.FromInt(p.ReadByte()));
            }
            catch
            {
            }

            return res;
        }
        [BuiltinFunction, CompatibleOS(OS.Windows)]
        public static var SerialReadTo(var port, var val)
        {
            var res = "";

            try
            {
                port.UseGCHandledData<SerialPort>(p => res = p.ReadTo(val));
            }
            catch
            {
                SetError(-1);
            }

            return res;
        }
        [BuiltinFunction]
        public static var SSHConnect(var host, var port, var user, var pass)
        {
            try
            {
                SshClient cl = new SshClient(host, port.ToInt(), user, pass);

                cl.Connect();

                return var.NewGCHandledData(cl);
            }
            catch
            {
                SetError(-1);

                return var.Null;
            }
        }
        [BuiltinFunction]
        public static var SSHClose(var conn) => Try(() => conn.UseDisposeGCHandledData<SshClient>(client => client.Disconnect()));
        [BuiltinFunction]
        public static var SSHCommandExitCode(var cmddata) => cmddata.UseGCHandledData((SshCommand cmd) => cmd.ExitStatus);
        [BuiltinFunction]
        public static var SSHCommandResult(var cmddata) => cmddata.UseGCHandledData((SshCommand cmd) => cmd.Result);
        [BuiltinFunction]
        public static var SSHCommandError(var cmddata) => cmddata.UseGCHandledData((SshCommand cmd) => cmd.Error);
        [BuiltinFunction]
        public static var SSHCommandDestroy(var cmddata) => Try(() => cmddata.UseGCHandledData((SshCommand cmd) => cmd.Dispose()));
        [BuiltinFunction]
        public static var SSHIsConnected(var conn) => conn.UseGCHandledData((SshClient client) => client.IsConnected);
        [BuiltinFunction]
        public static var SSHRunCommand(var conn, var cmd) => var.NewGCHandledData(conn.UseGCHandledData((SshClient client) => client.RunCommand(cmd)));
        [BuiltinFunction]
        public static var SSHRun(var conn, var cmd)
        {
            using (SshCommand sh = conn.UseGCHandledData((SshClient client) => client.RunCommand(cmd)))
                return sh.Result;
        }
        [BuiltinFunction]
        public static var SSHListDirectory(var conn, var dir)
        {
            SshCommand cmd = conn.UseGCHandledData((SshClient client) => client.RunCommand($"ls -AbC -w 1 \"{dir}\""));
            string[] files = cmd.Result.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            cmd.Dispose();

            return var.NewArray(files.Select(x => (var)x).ToArray());
        }
        [BuiltinFunction]
        public static var SSHUploadFile(var conn, var local, var remote) => Try(() =>
        {
            string b64 = Convert.ToBase64String(File.ReadAllBytes(local));

            SSHRun(conn, $"echo \"{b64}\" | base64 -d > \"{remote}\"");
        });
        [BuiltinFunction]
        public static var SSHDownloadFile(var conn, var local, var remote) => Try(() =>
        {
            var b64 = SSHRun(conn, $"cat \"{remote}\" | base64");

            File.WriteAllBytes(local, Convert.FromBase64String(b64));
        });
        [BuiltinFunction]
        public static var SSHDelete(var conn, var path) => Try(() => SSHCommand(conn, $"rm -rf \"{path}\""));
        [BuiltinFunction]
        public static var SSHCreateDirectory(var conn, var path) => Try(() => SSHCommand(conn, $"mkdir \"{path}\""));
        [BuiltinFunction]
        public static var SSHCreateFile(var conn, var path) => Try(() => SSHCommand(conn, $"touch \"{path}\""));
        [BuiltinFunction]
        public static var SSHReadFile(var conn, var path, var? codepage = null)
        {
            Encoding enc = Encoding.GetEncoding((codepage ?? Encoding.Default.CodePage).ToInt());
            var b64 = SSHRun(conn, $"cat \"{path}\" | base64");

            return enc.GetString(Convert.FromBase64String(b64));
        }
        [BuiltinFunction]
        public static var SSHWriteFile(var conn, var path, var content, var? codepage = null) => Try(() =>
        {
            Encoding enc = Encoding.GetEncoding((codepage ?? Encoding.Default.CodePage).ToInt());
            string b64 = Convert.ToBase64String(enc.GetBytes(content));

            SSHRun(conn, $"echo \"{b64}\" | base64 -d > \"{path}\"");
        });
        [BuiltinFunction]
        public static var StringExtract(var s, var s1, var s2, var? offs = null)
        {
            string inp = (s.ToString()).Substring((int)(offs ?? 0L));
            long i1 = inp.IndexOf(s1) + s1.Length;
            int i2 = inp.Substring((int)i1).IndexOf(s2);

            if (i2 >= 0)
            {
                SetError(0, i1);

                return inp.Substring((int)i1, i2);
            }
            else
                SetError(1, 0);

            return "";
        }
        [BuiltinFunction]
        public static dynamic TryConvertTo(var v, var type)
        {
            string tstr = type.ToLower();

            switch (tstr)
            {
                case "byte":
                    return v.ToByte();
                case "short":
                    return v.ToShort();
                case "int":
                    return v.ToInt();
                case "long":
                    return v.ToLong();
                case "float":
                    return (float)v.ToDouble();
                case "double":
                    return v.ToDouble();
                case "decimal":
                    return v.ToDecimal();
                case "dynamic":
                    return v.ToIntPtr();
            }

            if (tstr.StartsWith("void*"))
                return v.ToIntPtr();
            else if (tstr.Contains("stringbuilder"))
                return v.ToStringBuilder();
            else
                throw new NotImplementedException($"Cannot convert a variant to the type '{tstr}' (yet).");
        }
        [BuiltinFunction]
        public static var TryConvertFrom(dynamic v, var type)
        {
            string tstr = type.ToLower();

            switch (tstr)
            {
                case "byte":
                case "short":
                case "int":
                    return var.FromInt(v);
                case "long":
                    return var.FromLong(v);
                case "float":
                case "double":
                    return var.FromDouble(v);
                case "decimal":
                    return var.FromDecimal(v);
                case "dynamic":
                    return var.FromIntPtr(v);
            }

            if (tstr.StartsWith("void*"))
                return var.FromIntPtr((IntPtr)v);
            else if (tstr.Contains("stringbuilder"))
                return var.FromString(v?.ToString() ?? "");
            else
                throw new NotImplementedException($"Cannot convert the type '{tstr}' to a variant (yet).");
        }



        [BuiltinFunction, Warning("warnings.func_not_impl")]
        public static var FTPDownloadString(var host, var port, var user, var pass, var path) => throw new NotImplementedException(); // TODO
        [BuiltinFunction, Warning("warnings.func_not_impl")]
        public static var FTPDownloadFile(var host, var port, var user, var pass, var path, var target) => throw new NotImplementedException(); // TODO
        [BuiltinFunction, Warning("warnings.func_not_impl")]
        public static var FTPUploadString(var host, var port, var user, var pass, var path, var data) => throw new NotImplementedException(); // TODO
        [BuiltinFunction, Warning("warnings.func_not_impl")]
        public static var FTPUploadFile(var host, var port, var user, var pass, var path, var source) => throw new NotImplementedException(); // TODO


        #endregion

        // TODO : add all other functions from https://www.autoitscript.com/autoit3/docs/functions/
    }

    public struct Received
    {
        public IPEndPoint Sender { get; }
        public byte[] RawBytes { get; }
        public string Message { get; }


        public Received(byte[] bytes, IPEndPoint from) =>
            (RawBytes, Message, Sender) = (bytes, Encoding.Default.GetString(bytes), from);
    }

    public abstract class UDPBase
        : IDisposable
    {
        protected UdpClient _client = new UdpClient();


        protected internal UDPBase()
        {
        }

        public Received Receive()
        {
            UdpReceiveResult result = AsyncHelper.RunSync(_client.ReceiveAsync);

            return new Received(result.Buffer, result.RemoteEndPoint);
        }

        public void Close() => _client.Close();

        public void Dispose() => _client.Dispose();
    }

    public sealed class UDPListener
        : UDPBase
    {
        private readonly IPEndPoint _listenon;


        public UDPListener()
            : this(new IPEndPoint(IPAddress.Any, 31488))
        {
        }

        public UDPListener(IPEndPoint endpoint) => _client = new UdpClient(_listenon = endpoint) { EnableBroadcast = true };

        public void Reply(string message, IPEndPoint endpoint) => Reply(Encoding.Default.GetBytes(message), endpoint);

        public void Reply(byte[] bytes, IPEndPoint endpoint) => _client.Send(bytes, bytes.Length, endpoint);
    }

    public sealed class UDPUser
        : UDPBase
    {
        private UDPUser()
        {
        }

        public void Send(string message) => Send(Encoding.Default.GetBytes(message));

        public void Send(byte[] data) => _client.Send(data, data.Length);

        public static UDPUser ConnectTo(string hostname, int port, bool enable_broadcast)
        {
            UDPUser connection = new UDPUser();

            connection._client.EnableBroadcast = enable_broadcast;
            connection._client.Connect(hostname, port);

            return connection;
        }
    }

#pragma warning restore RCS1047, RCS1057, IDE1006

    public static class Shell
    {
        public static string Bash(this string cmd) => Run("/bin/bash", $"-c \"{cmd.Replace("\"", "\\\"")}\"");

        public static string Batch(this string cmd) => Run("cmd.exe", $"/c \"{cmd.Replace("\"", "\\\"")}\"");

        private static string Run(string filename, string arguments)
        {
            using (Process process = new Process()
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = filename,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = false,
                }
            })
            {
                process.Start();

                string result = process.StandardOutput.ReadToEnd();

                process.WaitForExit();

                return result;
            }
        }
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class BuiltinFunctionAttribute
        : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class CompatibleOSAttribute
        : Attribute
    {
        public OS[] Systems { get; }


        public CompatibleOSAttribute(params OS[] systems) => Systems = systems?.Distinct()?.ToArray() ?? new OS[0];
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class RequiresUnsafeAttribute
        : Attribute
    {
    }

#pragma warning disable RCS1203

    public abstract class CompilerIntrinsicMessage
        : Attribute
    {
        public string MessageName { get; }
        public object[] Arguments { get; }


        internal CompilerIntrinsicMessage(string name, params object[] args) =>
            (MessageName, Arguments) = (name, args);
    }

#pragma warning restore RCS1203

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public sealed class WarningAttribute
        : CompilerIntrinsicMessage
    {
        public WarningAttribute(string name, params object[] args)
            : base(name, args)
        {
        }
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public sealed class NoteAttribute
        : CompilerIntrinsicMessage
    {
        public NoteAttribute(string name, params object[] args)
            : base(name, args)
        {
        }
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class ObsoleteFunctionAttribute
        : CompilerIntrinsicMessage
    {
        public ObsoleteFunctionAttribute(bool crit, string func, params string[] repl)
            : base((crit ? "errors" : "warnings") + ".generator.obsolete_func", func, string.Join("', '", repl))
        {
        }
    }
}
