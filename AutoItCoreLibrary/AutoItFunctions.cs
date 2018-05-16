using System.Collections.Generic;
using System.Globalization;
using System.Diagnostics;
using System.Net.Sockets;
using System.Reflection;
using System.Linq;
using System.Text;
using System.Net;
using System.IO;
using System;

namespace AutoItCoreLibrary
{
    using System.Threading;
    using static Win32;

    using var = AutoItVariantType;


#pragma warning disable RCS1057
#pragma warning disable IDE1006
    public static unsafe class AutoItFunctions
    {
        public const string FUNC_PREFIX = "__userfunc_";
        public const string PINVOKE_PREFIX = "__pinvoke_";

        private static var __error;
        private static var __extended;

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

            return var.Default;
        }

        public static var __InvalidFunction__(params var[] _) =>
            throw new InvalidProgramException("The application tried to call an non-existing function ...");

        public static var DebugPrint(AutoItVariableDictionary vardic) => __(() =>
        {
            Console.WriteLine("globals:");

            foreach (string var in vardic._globals.Keys)
                Console.WriteLine($"    ${var} = \"{vardic._globals[var]}\"");

            if (vardic._locals.Count > 0)
            {
                Dictionary<string, var> topframe = vardic._locals.Peek();

                Console.WriteLine("locals:");

                foreach (string var in topframe.Keys)
                    Console.WriteLine($"    ${var} = \"{topframe[var]}\"");
            }
        });

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

        #endregion
        #region AutoIt3 compatible

        [BuiltinFunction]
        public static var Abs(var v) => v < 0 ? -v : v;
        [BuiltinFunction]
        public static var ACos(var v) => (var)Math.Acos((double)v);
        [BuiltinFunction, Warning("warnings.not_impl")]
        public static var AdlibRegister(var v) => throw new NotImplementedException(); // TODO
        [BuiltinFunction, Warning("warnings.not_impl")]
        public static var AdlibUnRegister(var v) => throw new NotImplementedException(); // TODO
        [BuiltinFunction]
        public static var Asc(var v) => v.Length > 0 ? v[0] > 'ÿ' ? '?' : v[0] : 0L;
        [BuiltinFunction]
        public static var AscW(var v) => v.Length > 0 ? v[0] : 0;
        [BuiltinFunction]
        public static var ASin(var v) => (var)Math.Asin((double)v);
        [BuiltinFunction]
        public static var Atan(var v) => (var)Math.Atan((double)v);
        [BuiltinFunction, Warning("warnings.not_impl")]
        public static var AutoItSetOption(var o, var? p = null) => throw new NotImplementedException(); // TODO
        [BuiltinFunction]
        public static var AutoItWinGetTitle() => throw new NotImplementedException(); // TODO
        [BuiltinFunction]
        public static var AutoItWinSetTitle(var v) => throw new NotImplementedException(); // TODO
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
        [BuiltinFunction, Warning("warnings.not_impl")]
        public static var BitRotate(var v, var? shift = null, var? size = null)
        {
            var offs = shift ?? 1;

            if (offs == 0)
                return v;
            else
                switch (size?.ToUpper() ?? "W")
                {
                    case "D":
                        return offs < 0 ? var.BitwiseRor(v, -offs) : var.BitwiseRol(v, offs);
                    case "W":
                        throw new NotImplementedException(); // TODO
                    case "B":
                        throw new NotImplementedException(); // TODO
                }

            throw new NotImplementedException(); // TODO
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

                return var.Default;
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
        [BuiltinFunction, Warning("warnings.not_impl")]
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



        [BuiltinFunction]
        public static var Floor(var v) => Math.Floor(v);



        [BuiltinFunction]
        public static var Min(var v1, var v2) => v1 <= v2 ? v1 : v2;
        [BuiltinFunction]
        public static var Max(var v1, var v2) => v1 >= v2 ? v1 : v2;


        [BuiltinFunction]
        public static var TCPAccept(var socket)
        {
            try
            {
                TcpClient client = null;

                socket.UseGCHandledData<TcpListener>(listener => client = listener.AcceptTcpClient());

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
                socket.UseDisposeGCHandledData(o =>
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
                return var.NewGCHandledData(new TcpListener(IPAddress.Parse(addr), port.ToInt()));
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
                SetError(ex.HResult, resp.Count == 0);
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
                SetError(ex.HResult);
            }

            return cnt;
        }
        [BuiltinFunction, Note("notes.unnecessary_function_comp", nameof(TCPStartup))]
        public static var TCPStartup() => 1;
        [BuiltinFunction, Note("notes.unnecessary_function_comp", nameof(TCPShutdown))]
        public static var TCPShutdown() => 1;



        [BuiltinFunction]
        public static var Sin(var v) => (var)Math.Sin((double)v);




        [BuiltinFunction, Note("notes.unnecessary_function_comp", nameof(UDPStartup))]
        public static var UDPStartup() => 1;
        [BuiltinFunction, Note("notes.unnecessary_function_comp", nameof(UDPShutdown))]
        public static var UDPShutdown() => 1;


        

        #endregion
        #region Additional functions

        [BuiltinFunction]
        public static var ATan2(var v1, var v2) => (var)Math.Atan2((double)v1, (double)v2);
        [BuiltinFunction]
        public static var ConsoleWriteLine(var v) => __(() => Console.WriteLine(v.ToString()));
        [BuiltinFunction]
        public static var ConsoleReadChar() => Console.ReadKey(true).KeyChar.ToString();
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
        public static var DnsGetIP(var v) => Dns.GetHostEntry(v).AddressList[0].ToString();
        [BuiltinFunction]
        public static var DnsGetName(var v) => Dns.GetHostEntry(v).HostName;
        [BuiltinFunction]
        public static var Identity(var v) => v;
        [BuiltinFunction]
        public static dynamic TryConvert(var v, var type)
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

        #endregion

        // TODO : add all other functions from https://www.autoitscript.com/autoit3/docs/functions/
    }
#pragma warning restore RCS1057
#pragma warning restore IDE1006

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
}
