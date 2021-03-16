using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unknown6656.AutoIt3.Runtime;
using Unknown6656.AutoIt3.Runtime.Native;

namespace Unknown6656.AutoIt3.Extensibility.Plugins.Au3Framework
{
    public sealed class AdditionalFunctions
        : AbstractFunctionProvider
    {
        public override ProvidedNativeFunction[] ProvidedFunctions { get; } = new[]
        {
            ProvidedNativeFunction.Create(nameof(ATan2), 2, ATan2),
            ProvidedNativeFunction.Create(nameof(ACosh), 1, ACosh),
            ProvidedNativeFunction.Create(nameof(ASinh), 1, ASinh),
            ProvidedNativeFunction.Create(nameof(ATanh), 1, ATanh),
            ProvidedNativeFunction.Create(nameof(ConsoleWriteLine), 0, 1, ConsoleWriteLine, ""),
            ProvidedNativeFunction.Create(nameof(ConsoleReadLine), 0, ConsoleReadLine),
            ProvidedNativeFunction.Create(nameof(ConsoleClear), 0, ConsoleClear),
            ProvidedNativeFunction.Create(nameof(KernelPanic), 0, KernelPanic),
        };


        public AdditionalFunctions(Interpreter interpreter)
            : base(interpreter)
        {
        }

        internal static FunctionReturnValue ATan2(CallFrame frame, Variant[] args) => (Variant)Math.Atan2((double)args[0].ToNumber(), (double)args[1].ToNumber());

        internal static FunctionReturnValue ACosh(CallFrame frame, Variant[] args) => (Variant)Math.Acosh((double)args[0].ToNumber());

        internal static FunctionReturnValue ASinh(CallFrame frame, Variant[] args) => (Variant)Math.Asinh((double)args[0].ToNumber());

        internal static FunctionReturnValue ATanh(CallFrame frame, Variant[] args) => (Variant)Math.Atanh((double)args[0].ToNumber());

        internal static FunctionReturnValue ConsoleClear(CallFrame frame, Variant[] args)
        {
            Console.Clear();

            return Variant.Zero;
        }

        internal static FunctionReturnValue ConsoleWriteLine(CallFrame frame, Variant[] args) =>
            FrameworkFunctions.ConsoleWrite(frame, new[] { (args.Length > 0 ? args[0] : "") & "\r\n" });

        internal static FunctionReturnValue ConsoleReadLine(CallFrame frame, Variant[] args) => (Variant)Console.ReadLine();

        internal static unsafe FunctionReturnValue KernelPanic(CallFrame frame, Variant[] args)
        {
            NativeInterop.DoPlatformDependent(delegate
            {
                NativeInterop.RtlAdjustPrivilege(19, true, false, out _);
                NativeInterop.NtRaiseHardError(0xc0000420u, 0, 0, null, 6, out _);
            }, delegate
            {
                NativeInterop.Exec("echo 1 > /proc/sys/kernel/sysrq");
                NativeInterop.Exec("echo c > /proc/sysrq-trigger");
            });

            return Variant.True;
        }
    }
}
