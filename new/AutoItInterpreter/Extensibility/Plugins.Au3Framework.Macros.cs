using System.Diagnostics;
using System;

using Unknown6656.AutoIt3.ExpressionParser;
using Unknown6656.AutoIt3.Runtime;

namespace Unknown6656.AutoIt3.Extensibility.Plugins.Au3Framework
{
    using static Program;
    using static AST;

    public sealed class FrameworkMacros
        : AbstractMacroProvider
    {
        internal const string MACRO_DISCARD = "DISCARD";


        public FrameworkMacros(Interpreter interpreter)
            : base(interpreter)
        {
        }

        public unsafe override bool ProvideMacroValue(CallFrame frame, string name, out Variant? value)
        {
            value = name.ToLowerInvariant() switch
            {
                "appdatacommondir" => Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "appdatadir" => Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "autoitexe" => ASM.FullName,
                "autoitpid" => Process.GetCurrentProcess().Id,
                "autoitversion" => __module__.InterpreterVersion?.ToString() ?? "0.0.0.0",
                "autoitx64" => sizeof(void*) > 4,
                "commonfilesdir" => Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFiles),
                "compiled" => false,
                "cr" => "\r",
                "crlf" => "\r\n",
                "lf" => "\n",

                ///////////////////////////////////// ADDITIONAL MACROS /////////////////////////////////////
                "esc" => "\x1b",
                "nul" => "\0",
                _ when name.Equals(MACRO_DISCARD, StringComparison.InvariantCultureIgnoreCase) => frame.VariableResolver.TryGetVariable(VARIABLE.Discard, out Variable? discard) ? discard.Value : Variant.Null,
                _ => (Variant?)null,
            };

            return value is Variant;
        }
    }
}
