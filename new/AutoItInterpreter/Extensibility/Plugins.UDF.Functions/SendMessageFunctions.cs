using Unknown6656.AutoIt3.Runtime.Native;
using Unknown6656.AutoIt3.Runtime;

namespace Unknown6656.AutoIt3.Extensibility.Plugins.UDF.Functions
{
    public sealed class SendMessageFunctions
        : AbstractFunctionProvider
    {
        public SendMessageFunctions(Interpreter interpreter)
            : base(interpreter)
        {
            RegisterFunction(nameof(_SendMessage), 2, 8, _SendMessage, OS.Windows, Variant.Zero, Variant.Zero, Variant.Zero, "wparam", "lparam", "lresult");
            RegisterFunction("_SendMessageA", 2, 8, _SendMessage, OS.Windows, Variant.Zero, Variant.Zero, Variant.Zero, "wparam", "lparam", "lresult");
            RegisterFunction("_SendMessageW", 2, 8, _SendMessage, OS.Windows, Variant.Zero, Variant.Zero, Variant.Zero, "wparam", "lparam", "lresult");
        }

        private static FunctionReturnValue _SendMessage(CallFrame frame, Variant[] args)
        {
            nint result;

            NativeInterop.SetLastError(0);

            if (args[3].IsString)
                result = NativeInterop.SendMessage((nint)args[0], (int)args[1], (nint)args[2], args[3].ToString());
            else
                result = NativeInterop.SendMessage((nint)args[0], (int)args[1], (nint)args[2], (nint)args[3]);

            if (NativeInterop.GetLastError() is int error and not 0)
                return FunctionReturnValue.Error(Variant.EmptyString, error, Variant.Zero);
            else
                return (int)args[4] switch
                {
                    int i and > 0 and <= 4 => args[i - 1],
                    _ => result
                };
        }
    }
}
