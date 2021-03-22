using Unknown6656.AutoIt3.Runtime;

namespace Unknown6656.AutoIt3.Extensibility
{
    public sealed class NETInteropFunctionProvider
        : AbstractFunctionProvider
    {
        public NETInteropFunctionProvider(Interpreter interpreter)
            : base(interpreter)
        {
            RegisterFunction(nameof(NETNew), 1, 256, NETNew); // <name> [opt-args]
            RegisterFunction(nameof(NETClass), 1, NETClass); // <name>
        }

        public static FunctionReturnValue NETNew(NativeCallFrame frame, Variant[] args)
        {
            args = frame.PassedArguments;

            bool result = frame.Interpreter.GlobalObjectStorage.TryCreateNETObject(args[0], args[1..], out Variant reference);

            if (result)
                return reference;
            else
                return FunctionReturnValue.Error(1);
        }

        public static FunctionReturnValue NETClass(NativeCallFrame frame, Variant[] args)
        {
            bool result = frame.Interpreter.GlobalObjectStorage.TryCreateNETStaticRefrence(args[0].ToString(), out Variant reference);

            if (result)
                return reference;
            else
                return FunctionReturnValue.Error(1);
        }
    }
}
