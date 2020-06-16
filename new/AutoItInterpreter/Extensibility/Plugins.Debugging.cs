using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Unknown6656.AutoIt3.Runtime;

namespace Unknown6656.AutoIt3.Extensibility.Plugins.Debugging
{
    public sealed class DebuggingFunctionProvider
        : AbstractFunctionProvider
    {
        public override ProvidedNativeFunction[] ProvidedFunctions { get; } = new[]
        {
            ProvidedNativeFunction.Create("DebugVar", 1, (frame, args) =>
            {
            }),
            // TODO : debug all vars
            // TODO : debug all threads
            // TODO : debug current thread
            // TODO : debug 
        };


        public DebuggingFunctionProvider(Interpreter interpreter)
            : base(interpreter)
        {
        }
    }
}
