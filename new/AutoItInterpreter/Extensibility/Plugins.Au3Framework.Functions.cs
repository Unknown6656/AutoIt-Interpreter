using System.Globalization;
using System.Linq;
using System;

using Unknown6656.AutoIt3.Runtime;
using Unknown6656.Common;
using Unknown6656.IO;
using System.IO;

namespace Unknown6656.AutoIt3.Extensibility.Plugins.Au3Framework
{
    public sealed class FrameworkFunctions
        : AbstractFunctionProvider
    {
        public override ProvidedNativeFunction[] ProvidedFunctions { get; } = new[]
        {
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
            ProvidedNativeFunction.Create(nameof(BinaryToString), 1, BinaryToString),
            ProvidedNativeFunction.Create(nameof(StringToBinary), 1, StringToBinary),
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
            ProvidedNativeFunction.Create(nameof(Assign), 2, 3, Assign),
            ProvidedNativeFunction.Create(nameof(IsArray), 1, IsArray),
            ProvidedNativeFunction.Create(nameof(IsBinary), 1, IsBinary),
            ProvidedNativeFunction.Create(nameof(IsBool), 1, IsBool),
            ProvidedNativeFunction.Create(nameof(IsFloat), 1, IsFloat),
            ProvidedNativeFunction.Create(nameof(IsFunc), 1, IsFunc),
            ProvidedNativeFunction.Create(nameof(IsInt), 1, IsInt),
            ProvidedNativeFunction.Create(nameof(IsKeyword), 1, IsKeyword),
            ProvidedNativeFunction.Create(nameof(IsNumber), 1, IsNumber),
            ProvidedNativeFunction.Create(nameof(IsObj), 1, IsObj),
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

        public static FunctionReturnValue BinaryToString(CallFrame frame, Variant[] args) => (Variant)From.Bytes(args[0].ToBinary()).To.String(BytewiseEncoding.Instance);

        public static FunctionReturnValue StringToBinary(CallFrame frame, Variant[] args) => (Variant)From.String(args[0].ToString(), BytewiseEncoding.Instance).To.Bytes;

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
                return frame.SetError(1, 0, -1m);
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
                return FunctionReturnValue.Error("", 1);
        }

        public static FunctionReturnValue EnvGet(CallFrame frame, Variant[] args) => Variant.FromObject(Environment.GetEnvironmentVariable(args[0].ToString()));

        public static FunctionReturnValue EnvSet(CallFrame frame, Variant[] args)
        {
            Environment.SetEnvironmentVariable(args[0].ToString(), args[1].IsDefault || args[1].IsNull ? null : args[1].ToString(), EnvironmentVariableTarget.Process);

            return Variant.True;
        }

        //public static FunctionReturnValue EnvUpdate(CallFrame frame, Variant[] args)
        //{
        //    Environment.
        //    return Variant.Null;
        //}

        public static FunctionReturnValue Execute(CallFrame frame, Variant[] args) =>
            GetAu3Caller(frame, nameof(Execute)).Match<FunctionReturnValue>(err => err, au3 => au3.ProcessAsVariant(args[0].ToString()));

        public static FunctionReturnValue Eval(CallFrame frame, Variant[] args) =>
            GetAu3Caller(frame, nameof(Execute)).Match<FunctionReturnValue>(err => err, au3 =>
            {
                if (au3.VariableResolver.TryGetVariable(args[0].ToString(), VariableSearchScope.Global, out Variable? variable))
                    return variable.Value;

                return InterpreterError.WellKnown(au3.CurrentLocation, "error.undeclared_variable", args[0]);
            });

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

        public static FunctionReturnValue IsObj(CallFrame frame, Variant[] args) => (Variant)(args[0].Type is VariantType.NETObject);

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

        [Flags]
        private enum AssignFlags
            : int
        {
            Create = 0,
            ForceLocal = 1,
            ForceGlobal = 2,
            ExistFail = 4
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
