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
            ProvidedNativeFunction.Create(nameof(Tan), 1, Tan),
            ProvidedNativeFunction.Create(nameof(Asc), 1, Asc),
            ProvidedNativeFunction.Create(nameof(AscW), 1, AscW),
            ProvidedNativeFunction.Create(nameof(Chr), 1, Chr),
            ProvidedNativeFunction.Create(nameof(ChrW), 1, ChrW),
            ProvidedNativeFunction.Create(nameof(Beep), 0, 2, Beep, 500m, 1000m),
            //ProvidedNativeFunction.Create(nameof(), 1, ),
            //ProvidedNativeFunction.Create(nameof(), 1, ),
            //ProvidedNativeFunction.Create(nameof(), 1, ),
            //ProvidedNativeFunction.Create(nameof(), 1, ),
            //ProvidedNativeFunction.Create(nameof(), 1, ),
            ProvidedNativeFunction.Create(nameof(ConsoleWrite), 1, ConsoleWrite),
            ProvidedNativeFunction.Create(nameof(ConsoleWriteError), 1, ConsoleWriteError),
            ProvidedNativeFunction.Create(nameof(ConsoleRead), 2, ConsoleRead),
            ProvidedNativeFunction.Create(nameof(MsgBox), 3, 5, MsgBox),
            ProvidedNativeFunction.Create(nameof(Execute), 1, Execute),
            ProvidedNativeFunction.Create(nameof(Eval), 1, Eval),
            ProvidedNativeFunction.Create(nameof(Assign), 2, 3, Assign),
            ProvidedNativeFunction.Create(nameof(IsDeclared), 1, IsDeclared),
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


        public static Union<InterpreterError, Variant> Abs(CallFrame frame, Variant[] args) => (Variant)Math.Abs(args[0].ToNumber());

        public static Union<InterpreterError, Variant> ACos(CallFrame frame, Variant[] args) => (Variant)Math.Acos((double)args[0].ToNumber());

        public static Union<InterpreterError, Variant> ASin(CallFrame frame, Variant[] args) => (Variant)Math.Asin((double)args[0].ToNumber());

        public static Union<InterpreterError, Variant> ATan(CallFrame frame, Variant[] args) => (Variant)Math.Atan((double)args[0].ToNumber());

        public static Union<InterpreterError, Variant> Cos(CallFrame frame, Variant[] args) => (Variant)Math.Cos((double)args[0].ToNumber());

        public static Union<InterpreterError, Variant> Sin(CallFrame frame, Variant[] args) => (Variant)Math.Sin((double)args[0].ToNumber());

        public static Union<InterpreterError, Variant> Tan(CallFrame frame, Variant[] args) => (Variant)Math.Tan((double)args[0].ToNumber());

        public static Union<InterpreterError, Variant> Asc(CallFrame frame, Variant[] args) => (Variant)(byte)args[0].ToString().FirstOrDefault();

        public static Union<InterpreterError, Variant> AscW(CallFrame frame, Variant[] args) => (Variant)(int)args[0].ToString().FirstOrDefault();

        public static Union<InterpreterError, Variant> Chr(CallFrame frame, Variant[] args) => (Variant)((char)(int)args[0]).ToString();

        public static Union<InterpreterError, Variant> ChrW(CallFrame frame, Variant[] args) => (Variant)((char)(byte)args[0]).ToString();

        public static Union<InterpreterError, Variant> Beep(CallFrame frame, Variant[] args)
        {
            Console.Beep((int)args[0], (int)args[1]);

            return Variant.True;
        }

        //public static Union<InterpreterError, Variant>(CallFrame frame, Variant[] args)
        //{
        //}

        //public static Union<InterpreterError, Variant>(CallFrame frame, Variant[] args)
        //{
        //}

        //public static Union<InterpreterError, Variant>(CallFrame frame, Variant[] args)
        //{
        //}

        //public static Union<InterpreterError, Variant>(CallFrame frame, Variant[] args)
        //{
        //}

        //public static Union<InterpreterError, Variant>(CallFrame frame, Variant[] args)
        //{
        //}

        public static Union<InterpreterError, Variant> ConsoleWriteError(CallFrame frame, Variant[] args)
        {
            string s = args[0].ToString();

            Console.Error.Write(s);

            return Variant.FromNumber(s.Length);
        }

        public static Union<InterpreterError, Variant> ConsoleWrite(CallFrame frame, Variant[] args)
        {
            string s = args[0].ToString();

            frame.Print(s);

            return Variant.FromNumber(s.Length);
        }

        public static Union<InterpreterError, Variant> ConsoleRead(CallFrame frame, Variant[] args)
        {
            bool peek = args[0].ToBoolean();
            bool binary = args[1].ToBoolean();

            // TODO

            throw new NotImplementedException();
        }

        public static Union<InterpreterError, Variant> MsgBox(CallFrame frame, Variant[] args)
        {
            decimal flag = args[0].ToNumber();
            string title = args[1].ToString();
            string text = args[2].ToString();
            decimal timeout = args[3].ToNumber();
            decimal hwnd = args[4].ToNumber();

            // TODO


            throw new NotImplementedException();
        }

        private static Union<InterpreterError, Variant> Execute(CallFrame frame, Variant[] args) =>
            GetAu3Caller(frame, nameof(Execute)).Match<Union<InterpreterError, Variant>>(err => err, au3 => au3.ProcessAsVariant(args[0].ToString()));

        private static Union<InterpreterError, Variant> Eval(CallFrame frame, Variant[] args) =>
            GetAu3Caller(frame, nameof(Execute)).Match<Union<InterpreterError, Variant>>(err => err, au3 =>
            {
                if (au3.VariableResolver.TryGetVariable(args[0].ToString(), out Variable? variable))
                    return variable.Value;

                return InterpreterError.WellKnown(au3.CurrentLocation, "error.undeclared_variable", args[0]);
            });

        private static Union<InterpreterError, Variant> Assign(CallFrame frame, Variant[] args) =>
            GetAu3Caller(frame, nameof(Execute)).Match<Union<InterpreterError, Variant>>(err => err, au3 =>
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

        private static Union<InterpreterError, Variant> IsDeclared(CallFrame frame, Variant[] args) =>
            GetAu3Caller(frame, nameof(Execute)).Match<Union<InterpreterError, Variant>>(err => err, au3 =>
            {
                if (au3.VariableResolver.TryGetVariable(args[0].ToString(), out Variable? variable))
                    return (Variant)(variable.IsGlobal ? 1 : -1);
                else
                    return Variant.Zero;
            });

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
            ProvidedNativeFunction.Create(nameof(ConsoleWriteLine), 0, 1, ConsoleWriteLine),
            ProvidedNativeFunction.Create(nameof(ConsoleReadLine), 0, ConsoleReadLine),
        };


        public AdditionalFunctions(Interpreter interpreter)
            : base(interpreter)
        {
    }

        public static Union<InterpreterError, Variant> ACosh(CallFrame frame, Variant[] args) => (Variant)Math.Acosh((double)args[0].ToNumber());

        public static Union<InterpreterError, Variant> ASinh(CallFrame frame, Variant[] args) => (Variant)Math.Asinh((double)args[0].ToNumber());

        public static Union<InterpreterError, Variant> ATanh(CallFrame frame, Variant[] args) => (Variant)Math.Atanh((double)args[0].ToNumber());

        public static Union<InterpreterError, Variant> ConsoleWriteLine(CallFrame frame, Variant[] args) => 
            FrameworkFunctions.ConsoleWrite(frame, new[] { (args.Length > 0 ? args[0] : "") & "\r\n" });

        public static Union<InterpreterError, Variant> ConsoleReadLine(CallFrame frame, Variant[] args) => (Variant)Console.ReadLine();
    }
}
