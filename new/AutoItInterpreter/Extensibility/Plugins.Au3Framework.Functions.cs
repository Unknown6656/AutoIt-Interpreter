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
            ProvidedNativeFunction.Create(nameof(ConsoleWrite), 1, ConsoleWrite),
            ProvidedNativeFunction.Create(nameof(ConsoleWriteError), 1, ConsoleWriteError),
            ProvidedNativeFunction.Create(nameof(ConsoleRead), 2, ConsoleRead),
            ProvidedNativeFunction.Create(nameof(MsgBox), (3, 5), MsgBox),
        };


        public FrameworkFunctions(Interpreter interpreter)
            : base(interpreter)
        {
        }

        public static Union<Variant, InterpreterError> ConsoleWriteError(CallFrame frame, Variant[] args)
        {
            string s = args[0].ToString();

            Console.Error.Write(s);

            return Variant.FromNumber(s.Length);
        }

        public static Union<Variant, InterpreterError> ConsoleWrite(CallFrame frame, Variant[] args)
        {
            string s = args[0].ToString();

            frame.Print(s);

            return Variant.FromNumber(s.Length);
        }

        public static Union<Variant, InterpreterError> ConsoleRead(CallFrame frame, Variant[] args)
        {
            bool peek = args[0].ToBoolean();
            bool binary = args[1].ToBoolean();

            // TODO

            throw new NotImplementedException();
        }

        public static Union<Variant, InterpreterError> MsgBox(CallFrame frame, Variant[] args)
        {
            decimal flag = args[0].ToNumber();
            string title = args[1].ToString();
            string text = args[2].ToString();
            decimal timeout = args[3].ToNumber();
            decimal hwnd = args[4].ToNumber();

            // TODO


            throw new NotImplementedException();
        }
    }

    public sealed class AdditionalFunctions
        : AbstractFunctionProvider
    {
        public override ProvidedNativeFunction[] ProvidedFunctions { get; } = new[]
        {
            ProvidedNativeFunction.Create(nameof(ConsoleWriteLine), (0, 1), ConsoleWriteLine),
            ProvidedNativeFunction.Create(nameof(ConsoleReadLine), 0, ConsoleReadLine),
        };


        public AdditionalFunctions(Interpreter interpreter)
            : base(interpreter)
        {
        }

        public static Union<Variant, InterpreterError> ConsoleWriteLine(CallFrame frame, Variant[] args) => 
            FrameworkFunctions.ConsoleWrite(frame, new[] { (args.Length > 0 ? args[0] : "") & "\r\n" });

        public static Union<Variant, InterpreterError> ConsoleReadLine(CallFrame frame, Variant[] args) => (Variant)Console.ReadLine();
    }
}
