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
    using var = AutoItVariantType;

#pragma warning disable RCS1057
#pragma warning disable IDE1006
    public static unsafe class AutoItFunctions
    {
        public const string FUNC_PREFIX = "__userfunc_";
        public const string PINVOKE_PREFIX = "__pinvoke_";

        private static var __error;
        private static var __extended;

        public static var @Error => __error;
        public static var @Extended => __extended;

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
            (Win32.System == OS.Windows ? windows : Win32.System == OS.Linux ? linux : macosx)?.Invoke();

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

            foreach (char c in $"{PINVOKE_PREFIX}_{dll}_{func}")
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
