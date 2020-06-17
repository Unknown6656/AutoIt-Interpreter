using System;
using System.Linq;
using System.Text;

using Unknown6656.AutoIt3.Runtime;
using Unknown6656.Common;

namespace Unknown6656.AutoIt3.Extensibility.Plugins.Debugging
{
    public sealed class FrameworkFunctions
        : AbstractFunctionProvider
    {
        public override ProvidedNativeFunction[] ProvidedFunctions { get; } = new[]
        {
            ProvidedNativeFunction.Create(nameof(ConsoleWrite), 1, ConsoleWrite),
            ProvidedNativeFunction.Create(nameof(ConsoleWriteLine), 1, ConsoleWriteLine),
            ProvidedNativeFunction.Create(nameof(ConsoleRead), 1, ConsoleRead),
            // TODO
        };


        public FrameworkFunctions(Interpreter interpreter)
            : base(interpreter)
        {
        }

        private static Union<Variant, InterpreterError> ConsoleWrite(CallFrame frame, Variant[] args)
        {
            return null;
        }

        private static Union<Variant, InterpreterError> ConsoleWriteLine(CallFrame frame, Variant[] args)
        {
            return null;
        }

        private static Union<Variant, InterpreterError> ConsoleRead(CallFrame frame, Variant[] args)
        {
            return null;
        }
    }
}
