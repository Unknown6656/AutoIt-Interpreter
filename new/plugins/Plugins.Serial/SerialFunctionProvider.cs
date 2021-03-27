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
        }
    }
}
