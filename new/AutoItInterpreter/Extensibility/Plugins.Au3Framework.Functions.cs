using System.Linq;
using System;

using Unknown6656.AutoIt3.Runtime;
using Unknown6656.Common;

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
            ProvidedNativeFunction.Create(nameof(Sqrt), 1, Sqrt),
            ProvidedNativeFunction.Create(nameof(Tan), 1, Tan),
            ProvidedNativeFunction.Create(nameof(Asc), 1, Asc),
            ProvidedNativeFunction.Create(nameof(AscW), 1, AscW),
            ProvidedNativeFunction.Create(nameof(Chr), 1, Chr),
            ProvidedNativeFunction.Create(nameof(ChrW), 1, ChrW),
            ProvidedNativeFunction.Create(nameof(Beep), 0, 2, Beep, 500m, 1000m),
            ProvidedNativeFunction.Create(nameof(BitAND), 2, 255, BitAND, Enumerable.Repeat((Variant)0xffffffff, 255).ToArray()),
            ProvidedNativeFunction.Create(nameof(BitOR), 2, 255, BitOR),
            ProvidedNativeFunction.Create(nameof(BitXOR), 2, 255, BitXOR),
            ProvidedNativeFunction.Create(nameof(BitNOT), 1, BitNOT),
            ProvidedNativeFunction.Create(nameof(BitShift), 2, BitShift),
            ProvidedNativeFunction.Create(nameof(BitRotate), 1, 3, BitRotate, 1, "W"),
            ProvidedNativeFunction.Create(nameof(ConsoleWrite), 1, ConsoleWrite),
            ProvidedNativeFunction.Create(nameof(ConsoleWriteError), 1, ConsoleWriteError),
            ProvidedNativeFunction.Create(nameof(ConsoleRead), 2, ConsoleRead),
            ProvidedNativeFunction.Create(nameof(MsgBox), 3, 5, MsgBox),
            ProvidedNativeFunction.Create(nameof(Execute), 1, Execute),
            ProvidedNativeFunction.Create(nameof(Eval), 1, Eval),
            ProvidedNativeFunction.Create(nameof(Assign), 2, 3, Assign),
            ProvidedNativeFunction.Create(nameof(IsDeclared), 1, IsDeclared),
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

        public static FunctionReturnValue Sqrt(CallFrame frame, Variant[] args) => (Variant)Math.Sqrt((double)args[0].ToNumber());

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

        public static FunctionReturnValue ConsoleWriteError(CallFrame frame, Variant[] args)
        {
            string s = args[0].ToString();

            Console.Error.Write(s);

            return Variant.FromNumber(s.Length);
        }

        public static FunctionReturnValue ConsoleWrite(CallFrame frame, Variant[] args)
        {
            string s = args[0].ToString();

            frame.Print(s);

            return Variant.FromNumber(s.Length);
        }

        public static FunctionReturnValue ConsoleRead(CallFrame frame, Variant[] args)
        {
            bool peek = args[0].ToBoolean();
            bool binary = args[1].ToBoolean();

            // TODO

            throw new NotImplementedException();
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

        public static FunctionReturnValue Execute(CallFrame frame, Variant[] args) =>
            GetAu3Caller(frame, nameof(Execute)).Match<FunctionReturnValue>(err => err, au3 => au3.ProcessAsVariant(args[0].ToString()));

        public static FunctionReturnValue Eval(CallFrame frame, Variant[] args) =>
            GetAu3Caller(frame, nameof(Execute)).Match<FunctionReturnValue>(err => err, au3 =>
            {
                if (au3.VariableResolver.TryGetVariable(args[0].ToString(), out Variable? variable))
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
                else if (scope.TryGetVariable(name, out variable!))
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
                if (au3.VariableResolver.TryGetVariable(args[0].ToString(), out Variable? variable))
                    return (Variant)(variable.IsGlobal ? 1 : -1);
                else
                    return Variant.Zero;
            });

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
        };


        public AdditionalFunctions(Interpreter interpreter)
            : base(interpreter)
        {
    }

        public static FunctionReturnValue ACosh(CallFrame frame, Variant[] args) => (Variant)Math.Acosh((double)args[0].ToNumber());

        public static FunctionReturnValue ASinh(CallFrame frame, Variant[] args) => (Variant)Math.Asinh((double)args[0].ToNumber());

        public static FunctionReturnValue ATanh(CallFrame frame, Variant[] args) => (Variant)Math.Atanh((double)args[0].ToNumber());

        public static FunctionReturnValue ConsoleWriteLine(CallFrame frame, Variant[] args) => 
            FrameworkFunctions.ConsoleWrite(frame, new[] { (args.Length > 0 ? args[0] : "") & "\r\n" });

        public static FunctionReturnValue ConsoleReadLine(CallFrame frame, Variant[] args) => (Variant)Console.ReadLine();
    }
}
