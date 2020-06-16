using System;

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
                if (args[0].AssignedTo is Variable var)
                    frame.Print($@"Variable '${var.Name}': {{
    Value:         {var.Value}
    Type:          {var.Value.Type}
    Raw Data:      ""{var.Value.RawData}"" ({var.Value.RawData?.GetType() ?? typeof(void)})
    Is Constant:   {var.IsConst}
    Is Global:     {var.IsGlobal}
    Decl.Location: {var.DeclaredLocation}
    Decl.Scope:    {var.DeclaredScope}
}}
");
                else
                    frame.Print($"Expression '{args[0]}' is not associated with any variable.\n");

                return null;
            }),
            ProvidedNativeFunction.Create("DebugCallFrame", 0, (frame, _) =>
            {
                frame.Print($@"
");

                return null;
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
