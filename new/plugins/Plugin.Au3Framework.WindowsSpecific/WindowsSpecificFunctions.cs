using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Unknown6656.AutoIt3.Extensibility;
using Unknown6656.AutoIt3.Runtime;
using Unknown6656.AutoIt3.Runtime.Native;

[assembly: AutoIt3Plugin]


namespace Unknown6656.AutoIt3.Extensibility.Plugins.Au3Framework
{
    public sealed class WindowsSpecificFunctions
        : AbstractFunctionProvider
    {
        public WindowsSpecificFunctions(Interpreter interpreter)
            : base(interpreter)
        {
        }
    }
}
