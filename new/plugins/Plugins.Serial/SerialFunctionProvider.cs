using System.IO.Ports;

using Unknown6656.AutoIt3.Extensibility;
using Unknown6656.AutoIt3.Runtime;

[assembly: AutoIt3Plugin]


namespace Unknown6656.AutoIt3.Extensibility.Plugins.Serial
{
    public sealed class SerialFunctionProvider
        : AbstractFunctionProvider
    {
        public SerialFunctionProvider(Interpreter interpreter)
            : base(interpreter)
        {
            RegisterFunction(nameof(SerialOpen), 2, 5, SerialOpen, new Variant[] { (int)Parity.None, 8, (int)StopBits.One });
            RegisterFunction(nameof(SerialClose), 1, SerialClose);
        }

        // SerialOpen( <port-name>, <baud-rate> [, <parity-bits> = 0 [, <data-bits> = 8 [, <stop-bits> = 1]]] )
        //  success: handle
        //  failure: zero, @error = -1
        private static FunctionReturnValue SerialOpen(CallFrame frame, Variant[] args)
        {
            try
            {
                return frame.Interpreter.GlobalObjectStorage.Store(new SerialPort(args[0].ToString(), (int)args[1], (Parity)(int)args[2], (int)args[3], (StopBits)(int)args[4]));
            }
            catch
            {
                return FunctionReturnValue.Error(-1);
            }
        }

        // SerialOpen( <handle> )
        private static FunctionReturnValue SerialClose(CallFrame frame, Variant[] args)
        {
            if (frame.Interpreter.GlobalObjectStorage.TryGet(args[0], out SerialPort? port))
            {
                port.Close();
                port.Dispose();

                frame.Interpreter.GlobalObjectStorage.Delete(args[0]);

                return Variant.True;
            }
            else
                return Variant.False;
        }

        // // Serial...( <handle> , ... )
        // private static FunctionReturnValue Serial...(CallFrame frame, Variant[] args)
        // {
        // }

        // // Serial...( <handle> , ... )
        // private static FunctionReturnValue Serial...(CallFrame frame, Variant[] args)
        // {
        // }

        // // Serial...( <handle> , ... )
        // private static FunctionReturnValue Serial...(CallFrame frame, Variant[] args)
        // {
        // }
    }
}
