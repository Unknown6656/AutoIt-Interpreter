using System.Linq;
using System.Text;

using Unknown6656.AutoIt3.Runtime;
using Unknown6656.Common;
using Unknown6656.IO;

namespace Unknown6656.AutoIt3.Extensibility.Plugins.Debugging
{
    public sealed class DebuggingFunctionProvider
        : AbstractFunctionProvider
    {
        public override ProvidedNativeFunction[] ProvidedFunctions { get; } = new[]
        {
            ProvidedNativeFunction.Create(nameof(DebugVar), 1, DebugVar),
            ProvidedNativeFunction.Create(nameof(DebugCallFrame), 0, DebugCallFrame),
            // TODO : debug all vars
            // TODO : debug all threads
            // TODO : debug current thread
            // TODO : debug 
        };


        public DebuggingFunctionProvider(Interpreter interpreter)
            : base(interpreter)
        {
        }

        private static Union<Variant, InterpreterError> DebugVar(CallFrame frame, Variant[] args)
        {
            int indentation = args.Length < 2 ? 0 : (int)args[1].ToNumber();
            StringBuilder sb = new StringBuilder();

            if (args[0].AssignedTo is Variable var)
                sb.Append($@"${var.Name} : {{
    Value:         {var.Value}
    Type:          {var.Value.Type}
    Raw Data:      ""{var.Value.RawData}"" ({var.Value.RawData?.GetType() ?? typeof(void)})
    Is Constant:   {var.IsConst}
    Is Global:     {var.IsGlobal}
    Decl.Location: {var.DeclaredLocation}
    Decl.Scope:    {var.DeclaredScope}
");
            else
                sb.Append($@"<unknown> : {{
    Value:         {args[0]}
    Type:          {args[0].Type}
    Raw Data:      ""{args[0].RawData}"" ({args[0].RawData?.GetType() ?? typeof(void)})
");

            if (args[0].ReferencedVariable is Variable @ref)
                sb.AppendLine($"    Reference to:  {DebugVar(frame, new[] { @ref.Value, Variant.FromNumber(indentation + 1) }).As<Variant>().ToString().Trim()}");

            sb.AppendLine("}");

            if (indentation == 0)
            {
                frame.Print(sb);

                return Variant.Null;
            }
            else
                return Variant.FromObject(string.Join("\n", From.String(sb.ToString()).To.Lines().Select(s => new string(' ', indentation * 4) + s)));
        }

        private static Union<Variant, InterpreterError> DebugCallFrame(CallFrame frame, Variant[] args)
        {
            StringBuilder sb = new StringBuilder();

            if (frame.CallerFrame is CallFrame caller)
            {
                Variable[] locals = caller.VariableResolver.LocalVariables;

                sb.Append($@"{caller.GetType().Name} : {{
    Thread:    {caller.CurrentThread}
    Function:  {caller.CurrentFunction}
    Ret.Value: {caller.ReturnValue}
    Variables: {locals.Length}");

                foreach (Variable var in locals)
                    sb.Append($@"
        ${var.Name} : {{
            Value:         {var.Value}
            Type:          {var.Value.Type}
            Raw Data:      ""{var.Value.RawData}"" ({var.Value.RawData?.GetType() ?? typeof(void)})
            Is Constant:   {var.IsConst}
            Is Global:     {var.IsGlobal}
            Decl.Location: {var.DeclaredLocation}
        }}");

                sb.Append($"\n    Arguments: {caller.PassedArguments.Length}");

                foreach (Variant arg in caller.PassedArguments)
                    sb.Append($"\n        \"{arg}\" ({arg.Type})");
            }
            else
                sb.AppendLine("<no call frame> : {");

            if (frame.CallerFrame is AU3CallFrame au3)
                sb.Append($@"
    Location:  {au3.CurrentLocation}
    Curr.Line: ""{au3.CurrentLineContent}""");

            sb.AppendLine("\n}");

            frame.Print(sb);

            return Variant.Null;
        }

        private static Union<Variant, InterpreterError> TODO(CallFrame frame, Variant[] args)
        {
            return Variant.Null;
        }
    }
}
