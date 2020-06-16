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
                    frame.Print($@"Variable '${var.Name}':
    Value:         {var.Value}
    Type:          {var.Value.Type}
    RawValue:      {var.Value.RawData}
    IsConstant:    {var.IsConst}
    IsGlobal:      {var.IsGlobal}
    Decl.Location: {var.DeclaredLocation}
    Decl.Scope:    {var.DeclaredScope}
");
                else
                    frame.Print($"Expression '{args[0]}' is not associated with any variable.");

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
