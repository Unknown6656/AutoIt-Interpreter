using System.Windows.Forms;

using Unknown6656.AutoIt3.Extensibility;
using Unknown6656.AutoIt3.Runtime.Native;
using Unknown6656.AutoIt3.Runtime;

[assembly: AutoIt3Plugin]


namespace Unknown6656.AutoIt3.Extensibility.Plugins.Au3Framework
{
    public sealed class WindowsSpecificFunctions
        : AbstractFunctionProvider
    {
        public WindowsSpecificFunctions(Interpreter interpreter)
            : base(interpreter)
        {
            RegisterFunction(nameof(ClipGet), 0, ClipGet, OS.Windows);
            RegisterFunction(nameof(ClipPut), 1, ClipPut);
        }

        private static FunctionReturnValue ClipGet(CallFrame frame, Variant[] args)
        {
            try
            {
                string value = Clipboard.GetText();

                if (string.IsNullOrEmpty(value))
                    return FunctionReturnValue.Error(1);
                else
                    return (Variant)value;
            }
            catch
            {
                return FunctionReturnValue.Error(Clipboard.ContainsText() ? 3 : 2);
            }
        }

        private static FunctionReturnValue ClipPut(CallFrame frame, Variant[] args)
        {
            try
            {
                return NativeInterop.DoPlatformDependent(delegate
                {
                    string value = args[0].ToString();

                    if (string.IsNullOrEmpty(value))
                        Clipboard.Clear();
                    else
                        Clipboard.SetText(value);

                    return Variant.True;
                }, () => Variant.FromBoolean(NativeInterop.Exec($"echo \"{args[0]}\" | pbcopy").code == 0));
            }
            catch
            {
                return Variant.False;
            }
        }
    }
}
