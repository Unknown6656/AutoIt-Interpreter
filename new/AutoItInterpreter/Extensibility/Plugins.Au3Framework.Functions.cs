using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.IO;
using System;

using Unknown6656.AutoIt3.Runtime.Native;
using Unknown6656.AutoIt3.Runtime;
using Unknown6656.Common;
using Unknown6656.IO;

namespace Unknown6656.AutoIt3.Extensibility.Plugins.Au3Framework
{
    public sealed class FrameworkFunctions
        : AbstractFunctionProvider
    {
        public override ProvidedNativeFunction[] ProvidedFunctions { get; } = new[]
        {
            ProvidedNativeFunction.Create(nameof(AutoItWinGetTitle), 0, AutoItWinGetTitle),
            ProvidedNativeFunction.Create(nameof(AutoItWinSetTitle), 1, AutoItWinSetTitle),
            ProvidedNativeFunction.Create(nameof(BlockInput), 1, BlockInput),
            ProvidedNativeFunction.Create(nameof(CDTray), 2, CDTray),
            ProvidedNativeFunction.Create(nameof(ClipPut), 1, ClipPut),
            ProvidedNativeFunction.Create(nameof(Abs), 1, Abs),
            ProvidedNativeFunction.Create(nameof(ACos), 1, ACos),
            ProvidedNativeFunction.Create(nameof(ASin), 1, ASin),
            ProvidedNativeFunction.Create(nameof(ATan), 1, ATan),
            ProvidedNativeFunction.Create(nameof(Cos), 1, Cos),
            ProvidedNativeFunction.Create(nameof(Sin), 1, Sin),
            ProvidedNativeFunction.Create(nameof(Exp), 1, Exp),
            ProvidedNativeFunction.Create(nameof(Mod), 2, Mod),
            ProvidedNativeFunction.Create(nameof(Log), 1, Log),
            ProvidedNativeFunction.Create(nameof(Sqrt), 1, Sqrt),
            ProvidedNativeFunction.Create(nameof(Floor), 1, Floor),
            ProvidedNativeFunction.Create(nameof(Ceiling), 1, Ceiling),
            ProvidedNativeFunction.Create(nameof(Round), 1, 2, Round),
            ProvidedNativeFunction.Create(nameof(Tan), 1, Tan),
            ProvidedNativeFunction.Create(nameof(Asc), 1, Asc),
            ProvidedNativeFunction.Create(nameof(AscW), 1, AscW),
            ProvidedNativeFunction.Create(nameof(Chr), 1, Chr),
            ProvidedNativeFunction.Create(nameof(ChrW), 1, ChrW),
            ProvidedNativeFunction.Create(nameof(Beep), 0, 2, Beep, 500m, 1000m),
            ProvidedNativeFunction.Create(nameof(BitAND), 2, 256, BitAND, Enumerable.Repeat((Variant)0xffffffff, 255).ToArray()),
            ProvidedNativeFunction.Create(nameof(BitOR), 2, 256, BitOR),
            ProvidedNativeFunction.Create(nameof(BitXOR), 2, 256, BitXOR),
            ProvidedNativeFunction.Create(nameof(BitNOT), 1, BitNOT),
            ProvidedNativeFunction.Create(nameof(BitShift), 2, BitShift),
            ProvidedNativeFunction.Create(nameof(BitRotate), 1, 3, BitRotate, 1, "W"),
            ProvidedNativeFunction.Create(nameof(String), 1, String),
            ProvidedNativeFunction.Create(nameof(Binary), 1, Binary),
            ProvidedNativeFunction.Create(nameof(BinaryLen), 1, BinaryLen),
            ProvidedNativeFunction.Create(nameof(BinaryMid), 2, 3, BinaryMid, Variant.Default),
            ProvidedNativeFunction.Create(nameof(Number), 1, Number),
            ProvidedNativeFunction.Create(nameof(Int), 1, Int),
            ProvidedNativeFunction.Create(nameof(Dec), 1, Dec),
            ProvidedNativeFunction.Create(nameof(Hex), 1, 2, Hex, Variant.Default),
            ProvidedNativeFunction.Create(nameof(BinaryToString), 1, 2, BinaryToString, 1),
            ProvidedNativeFunction.Create(nameof(StringToBinary), 1, 2, StringToBinary, 1),
            ProvidedNativeFunction.Create(nameof(Call), 1, 256, Call),
            ProvidedNativeFunction.Create(nameof(ConsoleWrite), 1, ConsoleWrite),
            ProvidedNativeFunction.Create(nameof(ConsoleWriteError), 1, ConsoleWriteError),
            ProvidedNativeFunction.Create(nameof(ConsoleRead), 2, ConsoleRead),
            ProvidedNativeFunction.Create(nameof(DirCreate), 1, DirCreate),
            ProvidedNativeFunction.Create(nameof(DirCopy), 2, 3, DirCopy, 0),
            ProvidedNativeFunction.Create(nameof(DirGetSize), 1, 2, DirGetSize, 0),
            ProvidedNativeFunction.Create(nameof(DirMove), 2, 3, DirMove, 0),
            ProvidedNativeFunction.Create(nameof(DirRemove), 1, 2, DirRemove, 0),
            ProvidedNativeFunction.Create(nameof(MsgBox), 3, 5, MsgBox),
            ProvidedNativeFunction.Create(nameof(EnvGet), 1, EnvGet),
            ProvidedNativeFunction.Create(nameof(EnvSet), 1, 2, EnvSet, Variant.Default),
            //ProvidedNativeFunction.Create(nameof(EnvUpdate), 0, EnvUpdate),
            ProvidedNativeFunction.Create(nameof(Execute), 1, Execute),
            ProvidedNativeFunction.Create(nameof(Eval), 1, Eval),
            ProvidedNativeFunction.Create(nameof(FileChangeDir), 1, FileChangeDir),
            ProvidedNativeFunction.Create(nameof(FileClose), 1, FileClose),
            ProvidedNativeFunction.Create(nameof(FileCopy), 2, 3, FileCopy, 0),
            ProvidedNativeFunction.Create(nameof(FileCreateNTFSLink), 2, 3, FileCreateNTFSLink, Variant.False),
            //ProvidedNativeFunction.Create(nameof(FileCreateShortcut), , FileCreateShortcut,),
            ProvidedNativeFunction.Create(nameof(FileDelete), 1, FileDelete),
            ProvidedNativeFunction.Create(nameof(FileExists), 1, FileExists),
            ProvidedNativeFunction.Create(nameof(FileFindFirstFile), 1, FileFindFirstFile),
            ProvidedNativeFunction.Create(nameof(FileFindNextFile), 1, 2, FileFindNextFile, Variant.False),
            ProvidedNativeFunction.Create(nameof(FileFlush), 1, FileFlush),
            ProvidedNativeFunction.Create(nameof(FileGetAttrib), 1, FileGetAttrib),
            ProvidedNativeFunction.Create(nameof(FileGetEncoding), 1, 2, FileGetEncoding, 1),
            ProvidedNativeFunction.Create(nameof(FileGetLongName), 1, FileGetLongName),
            ProvidedNativeFunction.Create(nameof(FileGetPos), 1, FileGetPos),
            //ProvidedNativeFunction.Create(nameof(FileGetShortcut), , FileGetShortcut),
            ProvidedNativeFunction.Create(nameof(FileGetShortName), 1, 2, FileGetShortName, 0),
            ProvidedNativeFunction.Create(nameof(FileGetSize), 1, FileGetSize),
            ProvidedNativeFunction.Create(nameof(FileGetTime), 1, 3, FileGetTime, 0, 0),
            //ProvidedNativeFunction.Create(nameof(FileGetVersion), , FileGetVersion),
            //ProvidedNativeFunction.Create(nameof(FileInstall), , FileInstall),
            ProvidedNativeFunction.Create(nameof(FileMove), 2, 3, FileMove, 0),
            ProvidedNativeFunction.Create(nameof(FileOpen), 1, 2, FileOpen, 0),
            //ProvidedNativeFunction.Create(nameof(FileOpenDialog), , FileOpenDialog),
            ProvidedNativeFunction.Create(nameof(FileRead), 1, 2, FileRead, Variant.Default),
            ProvidedNativeFunction.Create(nameof(FileReadLine), 1, 2, FileReadLine, 1),
            ProvidedNativeFunction.Create(nameof(FileReadToArray), 1, FileReadToArray),
            ProvidedNativeFunction.Create(nameof(FileRecycle), 1, FileRecycle),
            ProvidedNativeFunction.Create(nameof(FileRecycleEmpty), 0, 1, FileRecycleEmpty, Variant.Default),
            //ProvidedNativeFunction.Create(nameof(FileSaveDialog), , FileSaveDialog),
            //ProvidedNativeFunction.Create(nameof(FileSelectFolder), , FileSelectFolder),
            ProvidedNativeFunction.Create(nameof(FileSetAttrib), 2, 3 , FileSetAttrib, 0),
            ProvidedNativeFunction.Create(nameof(FileSetEnd), 1, FileSetEnd),
            ProvidedNativeFunction.Create(nameof(FileSetPos), 3, FileSetPos),
            ProvidedNativeFunction.Create(nameof(FileSetTime), 4, 2, FileSetTime, 0, 0),
            ProvidedNativeFunction.Create(nameof(FileWrite), 2, FileWrite),
            ProvidedNativeFunction.Create(nameof(FileWriteLine), 2, FileWriteLine),
            ProvidedNativeFunction.Create(nameof(Assign), 2, 3, Assign),
            ProvidedNativeFunction.Create(nameof(IsArray), 1, IsArray),
            ProvidedNativeFunction.Create(nameof(IsBinary), 1, IsBinary),
            ProvidedNativeFunction.Create(nameof(IsBool), 1, IsBool),
            ProvidedNativeFunction.Create(nameof(IsFloat), 1, IsFloat),
            ProvidedNativeFunction.Create(nameof(IsFunc), 1, IsFunc),
            ProvidedNativeFunction.Create(nameof(IsInt), 1, IsInt),
            ProvidedNativeFunction.Create(nameof(IsKeyword), 1, IsKeyword),
            ProvidedNativeFunction.Create(nameof(IsNumber), 1, IsNumber),
            //ProvidedNativeFunction.Create(nameof(IsObj), 1, IsObj),
            ProvidedNativeFunction.Create(nameof(IsString), 1, IsString),
            ProvidedNativeFunction.Create("StringIsFloat", 1, IsFloat),
            ProvidedNativeFunction.Create("StringIsInt", 1, IsInt),
            ProvidedNativeFunction.Create(nameof(StringIsDigit), 1, StringIsDigit),
            ProvidedNativeFunction.Create(nameof(StringIsAlNum), 1, StringIsAlNum),
            ProvidedNativeFunction.Create(nameof(StringIsAlpha), 1, StringIsAlpha),
            ProvidedNativeFunction.Create(nameof(StringIsASCII), 1, StringIsASCII),
            ProvidedNativeFunction.Create(nameof(StringIsLower), 1, StringIsLower),
            ProvidedNativeFunction.Create(nameof(StringIsSpace), 1, StringIsSpace),
            ProvidedNativeFunction.Create(nameof(StringIsUpper), 1, StringIsUpper),
            ProvidedNativeFunction.Create(nameof(StringIsXDigit), 1, StringIsXDigit),
            ProvidedNativeFunction.Create(nameof(IsDeclared), 1, IsDeclared),
            ProvidedNativeFunction.Create(nameof(FuncName), 1, FuncName),
            ProvidedNativeFunction.Create(nameof(SetError), 1, 3, SetError),
            ProvidedNativeFunction.Create(nameof(SetExtended), 1, 2, SetExtended),
        };

        public FrameworkFunctions(Interpreter interpreter)
            : base(interpreter)
        {
        }

        private static Union<InterpreterError, AU3CallFrame> GetAu3Caller(CallFrame? frame, string funcname)
        {
            AU3CallFrame? caller = null;

            while (frame is { })
                if (frame is AU3CallFrame au3)
                    caller = au3;
                else
                    frame = frame.CallerFrame;

            if (caller is { })
                return caller;
            else
                return InterpreterError.WellKnown(null, "error.au3_caller_only", funcname);
        }


        public static FunctionReturnValue AutoItWinGetTitle(CallFrame frame, Variant[] args) => (Variant)Console.Title;

        public static FunctionReturnValue AutoItWinSetTitle(CallFrame frame, Variant[] args)
        {
            Console.Title = args[0].ToString();

            return Variant.Null;
        }

        public static FunctionReturnValue BlockInput(CallFrame frame, Variant[] args) => NativeInterop.DoPlatformDependent<Variant>(() => NativeInterop.BlockInput(args[0].ToBoolean()), () => false);

        public static unsafe FunctionReturnValue CDTray(CallFrame frame, Variant[] args)
        {
            try
            {
                return NativeInterop.DoPlatformDependent<Variant>(delegate
                {
                    int dwbytes = 0;
                    void* cdrom = NativeInterop.CreateFile($"\\\\.\\{args[0]}", 0xc0000000u, 0, null, 3, 0, null);

                    return args[1].ToString().ToLower() switch
                    {
                        "open" => NativeInterop.DeviceIoControl(cdrom, 0x2d4808, null, 0, null, 0, &dwbytes, null),
                        "closed" => NativeInterop.DeviceIoControl(cdrom, 0x2d480c, null, 0, null, 0, &dwbytes, null),
                        _ => false
                    };
                }, delegate
                {
                    int cdrom = NativeInterop.open(args[0].ToString(), 0x0800);

                    return args[1].ToString().ToLower() switch
                    {
                        "open" or "closed" => NativeInterop.ioctl(cdrom, 0x5309, 0), // TODO ?
                        _ => false
                    };
                });
            }
            catch
            {
                return Variant.False;
            }
        }

        public static FunctionReturnValue ClipPut(CallFrame frame, Variant[] args)
        {
            try
            {
                string cmd = NativeInterop.DoPlatformDependent($"echo {args[0]} | clip", $"echo \"{args[0]}\" | pbcopy");
                (_, int code) = NativeInterop.Bash(cmd);

                return Variant.FromBoolean(code == 0);
            }
            catch
            {
                return Variant.False;
            }
        }

        public static FunctionReturnValue Abs(CallFrame frame, Variant[] args) => (Variant)Math.Abs(args[0].ToNumber());

        public static FunctionReturnValue ACos(CallFrame frame, Variant[] args) => (Variant)Math.Acos((double)args[0].ToNumber());

        public static FunctionReturnValue ASin(CallFrame frame, Variant[] args) => (Variant)Math.Asin((double)args[0].ToNumber());

        public static FunctionReturnValue ATan(CallFrame frame, Variant[] args) => (Variant)Math.Atan((double)args[0].ToNumber());

        public static FunctionReturnValue Cos(CallFrame frame, Variant[] args) => (Variant)Math.Cos((double)args[0].ToNumber());

        public static FunctionReturnValue Sin(CallFrame frame, Variant[] args) => (Variant)Math.Sin((double)args[0].ToNumber());

        public static FunctionReturnValue Tan(CallFrame frame, Variant[] args) => (Variant)Math.Tan((double)args[0].ToNumber());

        public static FunctionReturnValue Exp(CallFrame frame, Variant[] args) => (Variant)Math.Exp((double)args[0].ToNumber());

        public static FunctionReturnValue Log(CallFrame frame, Variant[] args) => (Variant)Math.Log((double)args[0].ToNumber());

        public static FunctionReturnValue Mod(CallFrame frame, Variant[] args) => args[0] % args[1];

        public static FunctionReturnValue Sqrt(CallFrame frame, Variant[] args) => (Variant)Math.Sqrt((double)args[0].ToNumber());

        public static FunctionReturnValue Floor(CallFrame frame, Variant[] args) => (Variant)Math.Floor(args[0].ToNumber());

        public static FunctionReturnValue Ceiling(CallFrame frame, Variant[] args) => (Variant)Math.Ceiling(args[0].ToNumber());

        public static FunctionReturnValue Round(CallFrame frame, Variant[] args) => (Variant)Math.Round(args[0].ToNumber(), (int)args[1]);

        public static FunctionReturnValue Asc(CallFrame frame, Variant[] args) => (Variant)(byte)args[0].ToString().FirstOrDefault();

        public static FunctionReturnValue AscW(CallFrame frame, Variant[] args) => (Variant)(int)args[0].ToString().FirstOrDefault();

        public static FunctionReturnValue Chr(CallFrame frame, Variant[] args) => (Variant)((char)(int)args[0]).ToString();

        public static FunctionReturnValue ChrW(CallFrame frame, Variant[] args) => (Variant)((char)(byte)args[0]).ToString();

        public static FunctionReturnValue Beep(CallFrame frame, Variant[] args)
        {
            Console.Beep((int)args[0], (int)args[1]);

            return Variant.True;
        }

        public static FunctionReturnValue BitAND(CallFrame frame, Variant[] args) => args.Aggregate(Variant.BitwiseAnd);

        public static FunctionReturnValue BitOR(CallFrame frame, Variant[] args) => args.Aggregate(Variant.BitwiseOr);

        public static FunctionReturnValue BitNOT(CallFrame frame, Variant[] args) => ~args[0];

        public static FunctionReturnValue BitXOR(CallFrame frame, Variant[] args) => args.Aggregate(Variant.BitwiseOr);

        public static FunctionReturnValue BitShift(CallFrame frame, Variant[] args) => args[0] >> (int)args[1];

        public static FunctionReturnValue BitRotate(CallFrame frame, Variant[] args)
        {
            Variant rotate(int size)
            {
                int amount = (int)args[1];
                long value = (long)args[0];
                long rotmask = -1L >> (64 - size);
                long @static = value & ~rotmask;
                long variable = value & rotmask;
                long rotated = (variable << amount) | (variable >> (size - amount));

                rotated &= rotmask;
                rotated |= @static;

                return rotated;
            }

            return args[2].ToString().ToUpperInvariant() switch
            {
                "B" => rotate(8),
                "W" => rotate(16),
                "D" => rotate(32),
                "Q" => rotate(64),
                _ => FunctionReturnValue.Error(-1),
            };
        }

        public static FunctionReturnValue Call(CallFrame frame, Variant[] args)
        {
            Variant[] call_args = frame.PassedArguments.Length == 2 &&
                                  args[1] is { Type: VariantType.Array } arr &&
                                  arr.TryGetIndexed(0, out Variant caa) &&
                                  caa.ToString().Equals("CallArgArray", StringComparison.InvariantCultureIgnoreCase) ? arr.ToArray() : args;

            call_args = call_args[1..];

            if (!args[0].IsFunction(out ScriptFunction? func))
                func = frame.Interpreter.ScriptScanner.TryResolveFunction(args[0].ToString());

            if (func is null)
                return FunctionReturnValue.Error(0xDEAD, 0xBEEF);
            else if (call_args.Length > func.ParameterCount.MaximumCount)
                Array.Resize(ref call_args, func.ParameterCount.MaximumCount);

            Union<InterpreterError, Variant> result = frame.Call(func, call_args);

            if (result.Is(out Variant @return))
                return @return;
            else
                return FunctionReturnValue.Error(0xDEAD, 0xBEEF);
        }

        public static FunctionReturnValue ConsoleWriteError(CallFrame frame, Variant[] args) =>
            frame.Interpreter.Telemetry.Measure<FunctionReturnValue>(TelemetryCategory.ScriptConsoleOut, delegate
            {
                string s = args[0].ToString();

                Console.Error.Write(s);

                return Variant.FromNumber(s.Length);
            });

        public static FunctionReturnValue ConsoleWrite(CallFrame frame, Variant[] args)
        {
            string s = args[0].ToString();

            frame.Print(s);

            return Variant.FromNumber(s.Length);
        }

        public static FunctionReturnValue ConsoleRead(CallFrame frame, Variant[] args) =>
            frame.Interpreter.Telemetry.Measure<FunctionReturnValue>(TelemetryCategory.ScriptConsoleIn, delegate
            {
                bool peek = args[0].ToBoolean();
                bool binary = args[1].ToBoolean();

                // TODO

                throw new NotImplementedException();
            });

        public static FunctionReturnValue String(CallFrame frame, Variant[] args) => (Variant)args[0].ToString();

        public static FunctionReturnValue Binary(CallFrame frame, Variant[] args) => (Variant)args[0].ToBinary();

        public static FunctionReturnValue BinaryLen(CallFrame frame, Variant[] args) => (Variant)args[0].ToBinary().Length;

        public static FunctionReturnValue BinaryMid(CallFrame frame, Variant[] args)
        {
            byte[] bytes = args[0].ToBinary();
            int start = (int)args[1] - 1;
            int count = (int)args[2];

            if (start < 0 || start >= bytes.Length)
                return Variant.EmptyBinary;
            else if (args[2].IsDefault)
                return (Variant)bytes[start..];
            else if (start + count > bytes.Length)
                return Variant.EmptyBinary;
            else
                return (Variant)bytes[start..(start + count)];
        }

        public static FunctionReturnValue Number(CallFrame frame, Variant[] args) => (Variant)(decimal)args[0];

        public static FunctionReturnValue Int(CallFrame frame, Variant[] args) => (Variant)(long)args[0];

        public static FunctionReturnValue BinaryToString(CallFrame frame, Variant[] args) => (Variant)From.Bytes(args[0].ToBinary()).To.String((int)args[1] switch
        {
            1 => Encoding.GetEncoding(1252),
            2 => Encoding.Unicode,
            3 => Encoding.BigEndianUnicode,
            4 => Encoding.UTF8,
            _ => BytewiseEncoding.Instance,
        });

        public static FunctionReturnValue StringToBinary(CallFrame frame, Variant[] args) => (Variant)From.String(args[0].ToString(), (int)args[1] switch
        {
            1 => Encoding.GetEncoding(1252),
            2 => Encoding.Unicode,
            3 => Encoding.BigEndianUnicode,
            4 => Encoding.UTF8,
            _ => BytewiseEncoding.Instance,
        }).To.Bytes;

        public static FunctionReturnValue Hex(CallFrame frame, Variant[] args)
        {
            byte[] bytes = args[0].ToBinary();
            int length = (int)args[1];

            if (args[0].Type is VariantType.Binary)
                return (Variant)From.Bytes(bytes).To.Hex();
            else if (!args[1].IsDefault && length < 1)
                return Variant.EmptyString;

            length = Math.Max(4, Math.Min(length, 16));

            if (bytes.Length < length)
                bytes = new byte[length - bytes.Length].Concat(bytes).ToArray();

            return (Variant)From.Bytes(bytes).To.Hex();
        }

        public static FunctionReturnValue Dec(CallFrame frame, Variant[] args) => (Variant)(long.TryParse(args[0].ToString(), NumberStyles.HexNumber, null, out long l) ? l : 0L);

        public static FunctionReturnValue DirCreate(CallFrame frame, Variant[] args)
        {
            try
            {
                DirectoryInfo dir = new DirectoryInfo(args[0].ToString());

                if (!dir.Exists)
                    dir.Create();


                return Variant.True;
            }
            catch
            {
                return Variant.False;
            }
        }

        public static FunctionReturnValue DirCopy(CallFrame frame, Variant[] args)
        {
            try
            {
                static void copy_rec(DirectoryInfo source, DirectoryInfo target, bool overwrite)
                {
                    foreach (DirectoryInfo dir in source.GetDirectories())
                        copy_rec(dir, target.CreateSubdirectory(dir.Name), overwrite);

                    foreach (FileInfo src in source.GetFiles())
                    {
                        string dest = Path.Combine(target.FullName, src.Name);

                        if (!File.Exists(dest) || overwrite)
                            src.CopyTo(dest, overwrite);
                    }
                }

                copy_rec(new DirectoryInfo(args[0].ToString()), new DirectoryInfo(args[1].ToString()), args[2].ToBoolean());

                return Variant.True;
            }
            catch
            {
                return Variant.False;
            }
        }

        public static FunctionReturnValue DirMove(CallFrame frame, Variant[] args)
        {
            try
            {
                static void move_rec(DirectoryInfo source, DirectoryInfo target, bool overwrite)
                {
                    foreach (DirectoryInfo dir in source.GetDirectories())
                        move_rec(dir, target.CreateSubdirectory(dir.Name), overwrite);

                    foreach (FileInfo src in source.GetFiles())
                    {
                        string dest = Path.Combine(target.FullName, src.Name);

                        if (!File.Exists(dest) || overwrite)
                            src.MoveTo(dest, overwrite);
                    }

                    source.Delete();
                }

                move_rec(new DirectoryInfo(args[0].ToString()), new DirectoryInfo(args[1].ToString()), args[2].ToBoolean());

                return Variant.True;
            }
            catch
            {
                return Variant.False;
            }
        }

        public static FunctionReturnValue DirRemove(CallFrame frame, Variant[] args)
        {
            try
            {
                static void delete_rec(DirectoryInfo dir, bool recursive)
                {
                    if (recursive)
                    {
                        foreach (DirectoryInfo sub in dir.GetDirectories())
                            delete_rec(sub, recursive);

                        foreach (FileInfo src in dir.GetFiles())
                            src.Delete();
                    }

                    dir.Delete();
                }

                delete_rec(new DirectoryInfo(args[0].ToString()), args[2].ToBoolean());

                return Variant.True;
            }
            catch
            {
                return Variant.False;
            }
        }

        public static FunctionReturnValue DirGetSize(CallFrame frame, Variant[] args)
        {
            try
            {
                long file_count = 0;
                long dir_count = 0;
                long total_size = 0;
                void count_rec(DirectoryInfo dir, bool recurse)
                {
                    if (recurse)
                        foreach (DirectoryInfo sub in dir.GetDirectories())
                        {
                            ++dir_count;

                            count_rec(sub, recurse);
                        }

                    foreach (FileInfo file in dir.GetFiles())
                    {
                        ++file_count;
                        total_size += file.Length;
                    }
                }

                count_rec(new DirectoryInfo(args[0].ToString()), args[2].ToNumber() is 0m or 1m);

                return args[2].ToNumber() is 1 ? Variant.FromArray(total_size, file_count, dir_count) : total_size;
            }
            catch
            {
                return FunctionReturnValue.Error(-1m, 1, 0);
            }
        }

        public static FunctionReturnValue MsgBox(CallFrame frame, Variant[] args)
        {
            decimal flag = args[0].ToNumber();
            string title = args[1].ToString();
            string text = args[2].ToString();
            decimal timeout = args[3].ToNumber();
            decimal hwnd = args[4].ToNumber();

            // TODO


            throw new NotImplementedException();
        }

        public static FunctionReturnValue FuncName(CallFrame frame, Variant[] args)
        {
            if (args[0].RawData is ScriptFunction func)
                return (Variant)func.Name;
            else
                return FunctionReturnValue.Error("", 1, 0);
        }

        public static FunctionReturnValue EnvGet(CallFrame frame, Variant[] args) => Variant.FromObject(Environment.GetEnvironmentVariable(args[0].ToString()));

        public static FunctionReturnValue EnvSet(CallFrame frame, Variant[] args)
        {
            Environment.SetEnvironmentVariable(args[0].ToString(), args[1].IsDefault || args[1].IsNull ? null : args[1].ToString(), EnvironmentVariableTarget.Process);

            return Variant.True;
        }

        // public static FunctionReturnValue EnvUpdate(CallFrame frame, Variant[] args)
        // {
        //     Environment.
        //     return Variant.Null;
        // }

        public static FunctionReturnValue Execute(CallFrame frame, Variant[] args) =>
            GetAu3Caller(frame, nameof(Execute)).Match<FunctionReturnValue>(err => err, au3 => au3.ProcessAsVariant(args[0].ToString()));

        public static FunctionReturnValue Eval(CallFrame frame, Variant[] args) =>
            GetAu3Caller(frame, nameof(Execute)).Match<FunctionReturnValue>(err => err, au3 =>
            {
                if (au3.VariableResolver.TryGetVariable(args[0].ToString(), VariableSearchScope.Global, out Variable? variable))
                    return variable.Value;

                return InterpreterError.WellKnown(au3.CurrentLocation, "error.undeclared_variable", args[0]);
            });

        public static FunctionReturnValue FileChangeDir(CallFrame frame, Variant[] args)
        {
            string curr = Directory.GetCurrentDirectory();

            Directory.SetCurrentDirectory(args[0].ToString());

            return Variant.FromBoolean(Directory.GetCurrentDirectory() != curr);
        }

        public static FunctionReturnValue FileClose(CallFrame frame, Variant[] args)
        {
            try
            {
                if (args[0].TryResolveHandle(frame.Interpreter, out FileHandle? handle))
                {
                    handle.Dispose();
                    frame.Interpreter.GlobalObjectStorage.Delete(args[0]);

                    return Variant.True;
                }
            }
            catch
            {
            }

            return Variant.False;
        }

        public static FunctionReturnValue FileCopy(CallFrame frame, Variant[] args)
        {
            try
            {
                FileInfo src = new FileInfo(args[0].ToString());
                FileInfo dst = new FileInfo(args[1].ToString());
                long flags = (long)args[2];

                if ((flags & 8) != 0 && dst.Directory is { Exists: false } dir)
                    dir.Create();

                src.CopyTo(dst.FullName, (flags & 1) != 0);

                return Variant.True;
            }
            catch
            {
                return Variant.False;
            }
        }

        public static FunctionReturnValue FileCreateNTFSLink(CallFrame frame, Variant[] args)
        {
            try
            {
                FileSystemInfo target = FileSystemExtensions.GetSystemInfo(args[0].ToString());
                FileSystemInfo linkname = FileSystemExtensions.GetSystemInfo(args[1].ToString());
                bool overwrite = (bool)args[2];

                if (overwrite && linkname.Exists)
                    linkname.Delete();

                return NativeInterop.DoPlatformDependent<Variant>(
                    () => FileSystemExtensions.CreateNTFSHardLink(linkname.FullName, target.FullName),
                    () => NativeInterop.Bash($"ln -f \"{linkname.FullName}\" \"{target.FullName}\"").code == 0
                );
            }
            catch
            {
                return Variant.False;
            }
        }

        // public static FunctionReturnValue FileCreateShortcut(CallFrame frame, Variant[] args)
        // {
        // }

        public static FunctionReturnValue FileDelete(CallFrame frame, Variant[] args)
        {
            string path = args[0].ToString();

            try
            {
                foreach (FileSystemInfo entry in FileSystemExtensions.ResolveWildCards(path))
                    if (entry.Exists)
                        if (entry is DirectoryInfo dir)
                            dir.Delete(true);
                        else
                            entry.Delete();

                return Variant.True;
            }
            catch
            {
                return Variant.False;
            }
        }

        public static FunctionReturnValue FileExists(CallFrame frame, Variant[] args) => Variant.FromBoolean(File.Exists(args[0].ToString()) || Directory.Exists(args[0].ToString()));

        public static FunctionReturnValue FileFindFirstFile(CallFrame frame, Variant[] args)
        {
            try
            {
                return (Variant)frame.Interpreter.GlobalObjectStorage.Store(new FileSearchHandle(FileSystemExtensions.ResolveWildCards(args[0].ToString())));
            }
            catch
            {
                return (Variant)(-1);
            }
        }

        public static FunctionReturnValue FileFindNextFile(CallFrame frame, Variant[] args)
        {
            try
            {
                if (args[0].TryResolveHandle(frame.Interpreter, out FileSearchHandle? handle))
                    if (handle.Enumerator.MoveNext())
                    {
                        FileSystemInfo fsi = handle.Enumerator.Current;
                        bool extended = (bool)args[1];

                        return FunctionReturnValue.Success(fsi.FullName, extended ? (Variant)GetAttributeString(fsi) : fsi.Attributes.HasFlag(FileAttributes.Directory));
                    }
                    else
                        frame.Interpreter.GlobalObjectStorage.Delete((int)args[0]);
            }
            catch
            {
            }

            return FunctionReturnValue.Error(1);
        }

        public static FunctionReturnValue FileFlush(CallFrame frame, Variant[] args)
        {
            try
            {
                if (args[0].TryResolveHandle(frame.Interpreter, out FileHandle? handle))
                {
                    handle.StreamWriter?.Flush();
                    handle.FileStream.Flush();

                    return Variant.True;
                }
            }
            catch
            {
            }

            return Variant.False;
        }

        public static FunctionReturnValue FileGetAttrib(CallFrame frame, Variant[] args)
        {
            try
            {
                return (Variant)GetAttributeString(FileSystemExtensions.GetSystemInfo(args[0].ToString()));
            }
            catch
            {
                return FunctionReturnValue.Error("", 1, 0);
            }
        }

        public static FunctionReturnValue FileGetEncoding(CallFrame frame, Variant[] args)
        {
            try
            {
                Encoding enc;

                if (args[0].TryResolveHandle(frame.Interpreter, out FileHandle? handle))
                {
                    if (handle.StreamReader.Peek() >= 0)
                    {
                        handle.StreamReader.Read();
                        handle.FileStream.Position--;
                    }

                    enc = handle.StreamReader.CurrentEncoding;
                }
                else
                {
                    using FileStream fs = File.OpenRead(args[0].ToString());
                    using StreamReader rd = new StreamReader(fs, Encoding.Default);

                    if (rd.Peek() >= 0)
                        rd.Read();

                    enc = rd.CurrentEncoding;
                }

                if (new Dictionary<int, FileOpenFlags>
                {
                    [new UnicodeEncoding(false, false).CodePage] = FileOpenFlags.FO_UTF16_LE_NOBOM,
                    [new UnicodeEncoding(true, false).CodePage] = FileOpenFlags.FO_UTF16_BE_NOBOM,
                    [new UTF8Encoding(true).CodePage] = FileOpenFlags.FO_UTF8,
                    [new UTF8Encoding(false).CodePage] = FileOpenFlags.FO_UTF8_NOBOM,
                    [Encoding.UTF8.CodePage] = FileOpenFlags.FO_UTF8,
                    [Encoding.Unicode.CodePage] = FileOpenFlags.FO_UTF16_LE,
                    [Encoding.BigEndianUnicode.CodePage] = FileOpenFlags.FO_UTF16_BE,
                    [1252] = FileOpenFlags.FO_ANSI,
                }.TryGetValue(enc.CodePage, out FileOpenFlags flags))
                    return (Variant)(int)flags;
            }
            catch
            {
            }

            return Variant.FromNumber(-1m);
        }

        public static FunctionReturnValue FileGetShortName(CallFrame frame, Variant[] args) => NativeInterop.DoPlatformDependent(
            delegate
            {
                try
                {
                    return (Variant)FileSystemExtensions.GetShortPath(args[1] == 1 ? args[0].ToString() : FileSystemExtensions.GetSystemInfo(args[0].ToString()).FullName);
                }
                catch
                {
                    return FunctionReturnValue.Error(1);
                }
            },
            () => args[0]
        );

        public static FunctionReturnValue FileGetPos(CallFrame frame, Variant[] args)
        {
            try
            {
                if (args[0].TryResolveHandle(frame.Interpreter, out FileHandle? handle))
                    return (Variant)handle.FileStream.Position;
            }
            catch
            {
            }

            return FunctionReturnValue.Error(0m, 1, 0);
        }

        // public static FunctionReturnValue FileGetShortcut(CallFrame frame, Variant[] args)
        // {
        // }

        public static FunctionReturnValue FileGetLongName(CallFrame frame, Variant[] args) => NativeInterop.DoPlatformDependent(
            delegate
            {
                try
                {
                    return (Variant)FileSystemExtensions.GetSystemInfo(args[0].ToString()).FullName;
                }
                catch
                {
                    return FunctionReturnValue.Error(1);
                }
            },
            () => args[0]
        );

        public static FunctionReturnValue FileGetSize(CallFrame frame, Variant[] args)
        {
            try
            {
                return (Variant)new FileInfo(args[0].ToString()).Length;
            }
            catch
            {
                return FunctionReturnValue.Error(1);
            }
        }

        public static FunctionReturnValue FileGetTime(CallFrame frame, Variant[] args)
        {
            try
            {
                FileSystemInfo file = new FileInfo(args[0].ToString());

                if (!file.Exists)
                    file = new DirectoryInfo(args[0].ToString());

                DateTime time = (int)args[1] switch
                {
                    1 => file.CreationTime,
                    2 => file.LastAccessTime,
                    _ => file.LastWriteTime,
                };

                return (int)args[2] switch
                {
                    1 => time.ToString("yyyyMMddHHmmss"),
                    _ => Variant.FromArray(
                        time.Year.ToString("D4"),
                        time.Month.ToString("D2"),
                        time.Day.ToString("D2"),
                        time.Hour.ToString("D2"),
                        time.Minute.ToString("D2"),
                        time.Second.ToString("D2")
                    ),
                };
            }
            catch
            {
                return FunctionReturnValue.Error(1);
            }
        }

        // public static FunctionReturnValue FileGetVersion(CallFrame frame, Variant[] args)
        // {
        // }

        // public static FunctionReturnValue FileInstall(CallFrame frame, Variant[] args)
        // {
        // }

        public static FunctionReturnValue FileMove(CallFrame frame, Variant[] args)
        {
            try
            {
                FileInfo src = new FileInfo(args[0].ToString());
                FileInfo dst = new FileInfo(args[1].ToString());
                long flags = (long)args[2];

                if ((flags & 8) != 0 && dst.Directory is { Exists: false } dir)
                    dir.Create();

                src.MoveTo(dst.FullName, (flags & 1) != 0);

                return Variant.True;
            }
            catch
            {
                return Variant.False;
            }
        }

        public static FunctionReturnValue FileOpen(CallFrame frame, Variant[] args)
        {
            try
            {
                FileInfo file = new FileInfo(args[0].ToString());
                FileOpenFlags flags = (FileOpenFlags)args[1].ToNumber();

                if (flags.HasFlag(FileOpenFlags.FO_CREATEPATH) && file.Directory is { Exists: false } dir)
                    dir.Create();

                FileStream fs = new FileStream(
                    file.FullName,
                    flags.HasFlag(FileOpenFlags.FO_OVERWRITE) ? FileMode.Create : flags.HasFlag(FileOpenFlags.FO_APPEND) ? FileMode.Append : FileMode.OpenOrCreate,
                    flags.HasFlag(FileOpenFlags.FO_OVERWRITE) || flags.HasFlag(FileOpenFlags.FO_APPEND) ? FileAccess.ReadWrite : FileAccess.Read, FileShare.Read
                );
                FileHandle handle = new FileHandle(fs, flags);

                return (Variant)frame.Interpreter.GlobalObjectStorage.Store(handle);
            }
            catch
            {
                return Variant.FromNumber(-1m);
            }
        }

        // public static FunctionReturnValue FileOpenDialog(CallFrame frame, Variant[] args)
        // {
        // }

        public static FunctionReturnValue FileRead(CallFrame frame, Variant[] args)
        {
            try
            {
                int? count = args[1] < 0 || args[1].IsDefault ? null : (int?)(int)args[1];
                Variant output;

                if (args[0].TryResolveHandle(frame.Interpreter, out FileHandle? handle))
                {
                    byte[] bytes = new byte[count ?? (handle.FileStream.Length - handle.FileStream.Position)];
                    int i = handle.FileStream.Read(bytes, 0, bytes.Length);

                    Array.Resize(ref bytes, i);

                    output = handle.Flags.HasFlag(FileOpenFlags.FO_BINARY) ? Variant.FromBinary(bytes) : Variant.FromString(From.Bytes(bytes).To.String(handle.Encoding));
                }
                else
                {
                    string s = File.ReadAllText(args[0].ToString());

                    if (count is int i && i < s.Length)
                        s = s[..i];

                    output = s;
                }

                frame.SetExtended(output.Length);

                if (count is int len && len > output.Length)
                    return FunctionReturnValue.Error(output, -1, output.Length); // eof

                return output;
            }
            catch
            {
                return FunctionReturnValue.Error(0m, 1, 0);
            }
        }

        public static FunctionReturnValue FileReadLine(CallFrame frame, Variant[] args)
        {
            try
            {
                Index? line_index = (int)args[1] switch
                {
                    0 => null,
                    _ when args[1].IsDefault => null,
                    int i when i < 0 => ^i,
                    int i => i - 1,
                };

                Variant output;
                bool eof = false;

                if (args[0].TryResolveHandle(frame.Interpreter, out FileHandle? handle))
                {
                    string s;

                    if (line_index is Index i)
                    {
                        List<string> lines = new List<string>();

                        handle.FileStream.Seek(0, SeekOrigin.Begin);

                        while (handle.StreamReader.ReadLine() is string line)
                            lines.Add(line);

                        s = lines[i];
                    }
                    else
                        s = handle.StreamReader.ReadLine() ?? "";

                    output = handle.Flags.HasFlag(FileOpenFlags.FO_BINARY) ? Variant.FromBinary(From.String(s, handle.Encoding).Data) : Variant.FromString(s);
                    eof = handle.FileStream.Position < handle.FileStream.Length - 1;
                }
                else
                    output = File.ReadAllLines(args[0].ToString())[line_index ?? 0];

                frame.SetExtended(output.Length);

                if (eof)
                    return FunctionReturnValue.Error(output, -1, output.Length); // eof

                return output;
            }
            catch
            {
                return FunctionReturnValue.Error(0m, 1, 0);
            }
        }

        public static FunctionReturnValue FileReadToArray(CallFrame frame, Variant[] args)
        {
            string[] lines = Array.Empty<string>();
            bool error = false;

            try
            {
                if (args[0].TryResolveHandle(frame.Interpreter, out FileHandle? handle))
                    lines = From.Stream(handle.FileStream).To.Lines();
                else
                    lines = File.ReadAllLines(args[0].ToString());
            }
            catch
            {
                error = true;
            }

            return FunctionReturnValue.Error(Variant.FromArray(lines.Select(Variant.FromString)), error ? 1 : lines.Length == 0 ? 2 : 0, lines.Length);
        }

        public static FunctionReturnValue FileRecycle(CallFrame frame, Variant[] args)
        {
            try
            {
                FileInfo file = new FileInfo(args[0].ToString());

                return NativeInterop.DoPlatformDependent(delegate
                {
                    SHFILEOPSTRUCT opt = new SHFILEOPSTRUCT
                    {
                        hwnd = null,
                        wFunc = FileFuncFlags.FO_DELETE,
                        pFrom = $"{file.FullName}\0\0",
                        pTo = null,
                        fFlags = FILEOP_FLAGS.FOF_SILENT | FILEOP_FLAGS.FOF_NOCONFIRMATION | FILEOP_FLAGS.FOF_NOERRORUI | FILEOP_FLAGS.FOF_ALLOWUNDO
                    };

                    return Variant.FromBoolean(NativeInterop.SHFileOperation(ref opt) == 0);
                }, delegate
                {
                    foreach (DirectoryInfo trash in new[]
                    {
                        "~/.local/share/Trash/items/",
                        "~/.local/share/Trash/",
                        "~/.Trash/",
                        "~/Desktop/.Trash/items/",
                        "~/Desktop/.Trash/",
                        "/var/tmp",
                    }.Select(p => new DirectoryInfo(p)))
                    {
                        if (trash.Exists)
                        {
                            file.MoveTo(Path.Combine(trash.FullName, file.Name));

                            return Variant.True;
                        }
                    }

                    return Variant.False;
                });
            }
            catch
            {
                return Variant.False;
            }
        }

        public static unsafe FunctionReturnValue FileRecycleEmpty(CallFrame frame, Variant[] args)
        {
            try
            {
                return NativeInterop.DoPlatformDependent(
                    () => Variant.FromBoolean(NativeInterop.SHEmptyRecycleBin(null, args[0].IsDefault ? null : args[0].ToString(), 0) == 0),
                    delegate
                    {
                        foreach (DirectoryInfo trash in new[]
                        {
                            "~/.local/share/Trash/items/",
                            "~/.local/share/Trash/",
                            "~/.Trash/",
                            "~/Desktop/.Trash/items/",
                            "~/Desktop/.Trash/",
                            "/tmp",
                        }.Select(p => new DirectoryInfo(p)))
                        {
                            if (trash.Exists)
                            {
                                trash.EnumerateDirectories().Do(d => d.Delete(true));
                                trash.EnumerateFiles().Do(f => f.Delete());
                            }
                        }

                        return Variant.False;
                    }
                );
            }
            catch
            {
                return Variant.False;
            }
        }

        // public static FunctionReturnValue FileSaveDialog(CallFrame frame, Variant[] args)
        // {
        // }

        // public static FunctionReturnValue FileSelectFolder(CallFrame frame, Variant[] args)
        // {
        // }

        public static FunctionReturnValue FileSetAttrib(CallFrame frame, Variant[] args)
        {
            try
            {
                string flags = args[1].ToString();
                FileAttributes pos = default;
                FileAttributes neg = default;
                bool mode_pos = true;

                foreach (char c in flags)
                {
                    if (char.IsWhiteSpace(c))
                        continue;
                    else if (c is '+')
                        mode_pos = true;
                    else if (c is '-')
                        mode_pos = false;
                    else
                        (mode_pos ? ref pos : ref neg) |= c switch
                        {
                            'R' => FileAttributes.ReadOnly,
                            'A' => FileAttributes.Archive,
                            'S' => FileAttributes.System,
                            'H' => FileAttributes.Hidden,
                            'N' => FileAttributes.Normal,
                            'O' => FileAttributes.Offline,
                            'T' => FileAttributes.Temporary,
                            _ => throw new ArgumentException(),
                        };
                }

                void traverse(FileSystemInfo fsi)
                {
                    if (fsi is DirectoryInfo dir && (int)args[3] == 1)
                        foreach (FileSystemInfo item in dir.EnumerateFileSystemInfos())
                            traverse(item);

                    fsi.Attributes = (fsi.Attributes & ~neg) | pos;
                };

                FileSystemExtensions.ResolveWildCards(args[0].ToString()).Do(traverse);

                return Variant.True;
            }
            catch
            {
                return Variant.False;
            }
        }

        public static FunctionReturnValue FileSetEnd(CallFrame frame, Variant[] args)
        {
            try
            {
                if (args[0].TryResolveHandle(frame.Interpreter, out FileHandle? handle))
                {
                    handle.FileStream.Seek(0, SeekOrigin.End);

                    return Variant.True;
                }
            }
            catch
            {
            }

            return Variant.False;
        }

        public static FunctionReturnValue FileSetPos(CallFrame frame, Variant[] args)
        {
            try
            {
                if (args[0].TryResolveHandle(frame.Interpreter, out FileHandle? handle))
                {
                    handle.FileStream.Seek((long)args[1], (SeekOrigin)(int)args[2]);

                    return Variant.True;
                }
            }
            catch
            {
            }

            return Variant.False;
        }

        public static FunctionReturnValue FileSetTime(CallFrame frame, Variant[] args)
        {
            try
            {
                DateTime time;

                if (!DateTime.TryParseExact(args[1].ToString(), "yyyyMMddHHmmss", null, DateTimeStyles.AssumeLocal, out time))
                    time = DateTime.Now;

                void traverse(FileSystemInfo fsi)
                {
                    if (fsi is DirectoryInfo dir && (int)args[3] == 1)
                        foreach (FileSystemInfo item in dir.EnumerateFileSystemInfos())
                            traverse(item);

                    if (args[2] == 1m)
                        fsi.CreationTime = time;
                    else if (args[2] == 2m)
                        fsi.LastAccessTime = time;
                    else
                        fsi.LastWriteTime = time;
                }

                FileSystemExtensions.ResolveWildCards(args[0].ToString()).Do(traverse);

                return Variant.True;
            }
            catch
            {
                return Variant.False;
            }
        }

        public static FunctionReturnValue FileWrite(CallFrame frame, Variant[] args)
        {
            try
            {
                byte[] data = args[1].IsBinary ? args[1].ToBinary() : From.String(args[1].ToString(), Encoding.UTF8).To.Bytes;

                if (args[0].TryResolveHandle(frame.Interpreter, out FileHandle? handle))
                    handle.FileStream.Write(data, 0, data.Length);
                else
                    File.WriteAllBytes(args[0].ToString(), data);

                return Variant.True;
            }
            catch
            {
                return Variant.False;
            }
        }

        public static FunctionReturnValue FileWriteLine(CallFrame frame, Variant[] args)
        {
            try
            {
                string content = args[1].ToString();

                if (!content.EndsWith("\r") && !content.EndsWith("\n"))
                    content += "\r\n";

                if (args[0].TryResolveHandle(frame.Interpreter, out FileHandle? handle))
                {
                    if (handle.StreamWriter is null)
                        return Variant.False;
                    else
                        handle.StreamWriter?.Write(content);
                }
                else
                    File.WriteAllText(args[0].ToString(), content);

                return Variant.True;
            }
            catch
            {
                return Variant.False;
            }
        }

        public static FunctionReturnValue Assign(CallFrame frame, Variant[] args) =>
            GetAu3Caller(frame, nameof(Execute)).Match<FunctionReturnValue>(err => err, au3 =>
            {
                string name = args[0].ToString();
                Variant data = args[1];
                AssignFlags flags = (AssignFlags)(int)args[2];
                VariableScope scope = flags.HasFlag(AssignFlags.ForceGlobal) ? au3.VariableResolver.GlobalRoot : au3.VariableResolver;
                Variable variable;

                if (flags.HasFlag(AssignFlags.ForceGlobal) && flags.HasFlag(AssignFlags.ForceLocal))
                    return Variant.False;
                else if (scope.TryGetVariable(name, flags.HasFlag(AssignFlags.ForceLocal) ? VariableSearchScope.Local : VariableSearchScope.Global, out variable!))
                {
                    if (variable.IsConst || flags.HasFlag(AssignFlags.ExistFail))
                        return Variant.False;
                }
                else if (flags.HasFlag(AssignFlags.Create))
                    variable = scope.CreateVariable(au3.CurrentLocation, name, false);
                else
                    return Variant.False;

                variable.Value = data;

                return Variant.True;
            });

        public static FunctionReturnValue IsDeclared(CallFrame frame, Variant[] args) =>
            GetAu3Caller(frame, nameof(Execute)).Match<FunctionReturnValue>(err => err, au3 =>
            {
                if (au3.VariableResolver.TryGetVariable(args[0].ToString(), VariableSearchScope.Global, out Variable? variable))
                    return (Variant)(variable.IsGlobal ? 1 : -1);
                else
                    return Variant.Zero;
            });

        public static FunctionReturnValue IsArray(CallFrame frame, Variant[] args) => (Variant)(args[0].Type is VariantType.Array);

        public static FunctionReturnValue IsBinary(CallFrame frame, Variant[] args) => (Variant)(args[0].Type is VariantType.Binary);

        public static FunctionReturnValue IsBool(CallFrame frame, Variant[] args) => (Variant)(args[0].Type is VariantType.Boolean);

        public static FunctionReturnValue IsFloat(CallFrame frame, Variant[] args) => (Variant)(args[0].ToNumber() is decimal d && (long)d != d);

        public static FunctionReturnValue IsFunc(CallFrame frame, Variant[] args) => (Variant)(args[0].Type is VariantType.Function);

        public static FunctionReturnValue IsInt(CallFrame frame, Variant[] args) => (Variant)(args[0].ToNumber() is decimal d && (long)d == d);

        public static FunctionReturnValue IsKeyword(CallFrame frame, Variant[] args) => (Variant)(args[0].IsDefault ? 1m : args[0].IsNull ? 2m : 0m);

        public static FunctionReturnValue IsNumber(CallFrame frame, Variant[] args) => (Variant)(args[0].Type is VariantType.Number);

        //public static FunctionReturnValue IsObj(CallFrame frame, Variant[] args) => (Variant)(args[0].Type is VariantType.NETObject);

        public static FunctionReturnValue IsString(CallFrame frame, Variant[] args) => (Variant)(args[0].Type is VariantType.String);

        public static FunctionReturnValue StringIsDigit(CallFrame frame, Variant[] args) => (Variant)args[0].ToString().All(char.IsDigit);

        public static FunctionReturnValue StringIsAlNum(CallFrame frame, Variant[] args) => (Variant)args[0].ToString().All(char.IsLetterOrDigit);

        public static FunctionReturnValue StringIsAlpha(CallFrame frame, Variant[] args) => (Variant)args[0].ToString().All(char.IsLetter);

        public static FunctionReturnValue StringIsASCII(CallFrame frame, Variant[] args) => (Variant)args[0].ToString().All(c => c < 0x80);

        public static FunctionReturnValue StringIsLower(CallFrame frame, Variant[] args) => (Variant)args[0].ToString().All(char.IsLower);

        public static FunctionReturnValue StringIsSpace(CallFrame frame, Variant[] args) => (Variant)args[0].ToString().All(char.IsWhiteSpace);

        public static FunctionReturnValue StringIsUpper(CallFrame frame, Variant[] args) => (Variant)args[0].ToString().All(char.IsUpper);

        public static FunctionReturnValue StringIsXDigit(CallFrame frame, Variant[] args) => (Variant)args[0].ToString().All("0123456789abcdefABCDEF".Contains);

        public static FunctionReturnValue SetExtended(CallFrame frame, Variant[] args) => frame.SetExtended((int)args[0], args[1]);

        public static FunctionReturnValue SetError(CallFrame frame, Variant[] args) => frame.SetError((int)args[0], (int)args[1], args[2]);

        private static string GetAttributeString(FileSystemInfo info)
        {
            StringBuilder sb = new StringBuilder();
            FileAttributes attr = info.Attributes;
            Dictionary<FileAttributes, char> dic = new()
            {
                [FileAttributes.ReadOnly] = 'R',
                [FileAttributes.Hidden] = 'H',
                [FileAttributes.System] = 'S',
                [FileAttributes.Directory] = 'D',
                [FileAttributes.Archive] = 'A',
                [FileAttributes.Normal] = 'N',
                [FileAttributes.Temporary] = 'T',
                [FileAttributes.Compressed] = 'C',
                [FileAttributes.Offline] = 'O',
                [FileAttributes.Encrypted] = 'X',
                // the following are optional
                [FileAttributes.Device] = 'V',
                [FileAttributes.IntegrityStream] = 'I',
                [FileAttributes.ReparsePoint] = 'P',
                [FileAttributes.NotContentIndexed] = 'Z',
            };

            foreach (FileAttributes key in dic.Keys)
                if (attr.HasFlag(key))
                    sb.Append(dic[key]);

            return sb.ToString();
        }

        [Flags]
        private enum AssignFlags
            : int
        {
            Create = 0,
            ForceLocal = 1,
            ForceGlobal = 2,
            ExistFail = 4
        }

        [Flags]
        private enum FileOpenFlags
        {
            FO_READ = 0,
            FO_APPEND = 1,
            FO_OVERWRITE = 2,
            FO_CREATEPATH = 8,
            FO_BINARY = 16,
            FO_UNICODE = 32,
            FO_UTF16_LE = FO_UNICODE,
            FO_UTF16_BE = 64,
            FO_UTF8 = 128,
            FO_UTF8_NOBOM =256,
            FO_ANSI = 512,
            FO_UTF16_LE_NOBOM = 1024,
            FO_UTF16_BE_NOBOM = 2048,
            FO_FULLFILE_DETECT = 16384,
        }

        private sealed class FileSearchHandle
        {
            public IEnumerator<FileSystemInfo> Enumerator {get; }


            public FileSearchHandle(FileSystemInfo[] files) => Enumerator = (IEnumerator<FileSystemInfo>)files.GetEnumerator();
        }

        private sealed class FileHandle
            : IDisposable
        {
            public FileOpenFlags Flags { get; }
            public FileStream FileStream { get; }
            public StreamReader StreamReader { get; }
            public StreamWriter? StreamWriter { get; }
            public Encoding Encoding { get; }


            public FileHandle(FileStream fs, FileOpenFlags flags)
            {
                Flags = flags;
                FileStream = fs;
                Encoding = flags.HasFlag(FileOpenFlags.FO_UNICODE) || flags.HasFlag(FileOpenFlags.FO_UTF16_LE) ? Encoding.Unicode :
                           flags.HasFlag(FileOpenFlags.FO_UTF16_BE) ? Encoding.BigEndianUnicode :
                           flags.HasFlag(FileOpenFlags.FO_UTF8) ? new UTF8Encoding(true) :
                           flags.HasFlag(FileOpenFlags.FO_UTF8_NOBOM) ? new UTF8Encoding(false) :
                           flags.HasFlag(FileOpenFlags.FO_ANSI) ? Encoding.GetEncoding(1252) :
                           flags.HasFlag(FileOpenFlags.FO_UTF16_LE_NOBOM) ? new UnicodeEncoding(false, false) :
                           flags.HasFlag(FileOpenFlags.FO_UTF16_BE_NOBOM) ? new UnicodeEncoding(true, false) : Encoding.Default;

                StreamReader = new(fs, Encoding);

                if (fs.CanWrite)
                    StreamWriter = new(fs, Encoding);
            }

            public void Dispose()
            {
                StreamWriter?.Close();
                StreamWriter?.Dispose();
                StreamReader.Close();
                StreamReader.Dispose();
                FileStream.Close();
                FileStream.Dispose();
            }
        }
    }

    public sealed class AdditionalFunctions
        : AbstractFunctionProvider
    {
        public override ProvidedNativeFunction[] ProvidedFunctions { get; } = new[]
        {
            ProvidedNativeFunction.Create(nameof(ACosh), 1, ACosh),
            ProvidedNativeFunction.Create(nameof(ASinh), 1, ASinh),
            ProvidedNativeFunction.Create(nameof(ATanh), 1, ATanh),
            ProvidedNativeFunction.Create(nameof(ConsoleWriteLine), 0, 1, ConsoleWriteLine, ""),
            ProvidedNativeFunction.Create(nameof(ConsoleReadLine), 0, ConsoleReadLine),
            ProvidedNativeFunction.Create(nameof(ConsoleClear), 0, ConsoleClear),
        };


        public AdditionalFunctions(Interpreter interpreter)
            : base(interpreter)
        {
        }

        public static FunctionReturnValue ACosh(CallFrame frame, Variant[] args) => (Variant)Math.Acosh((double)args[0].ToNumber());

        public static FunctionReturnValue ASinh(CallFrame frame, Variant[] args) => (Variant)Math.Asinh((double)args[0].ToNumber());

        public static FunctionReturnValue ATanh(CallFrame frame, Variant[] args) => (Variant)Math.Atanh((double)args[0].ToNumber());

        public static FunctionReturnValue ConsoleClear(CallFrame frame, Variant[] args)
        {
            Console.Clear();

            return Variant.Zero;
        }

        public static FunctionReturnValue ConsoleWriteLine(CallFrame frame, Variant[] args) =>
            FrameworkFunctions.ConsoleWrite(frame, new[] { (args.Length > 0 ? args[0] : "") & "\r\n" });

        public static FunctionReturnValue ConsoleReadLine(CallFrame frame, Variant[] args) => (Variant)Console.ReadLine();
    }
}
