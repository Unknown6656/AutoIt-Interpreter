using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
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
            ProvidedNativeFunction.Create(nameof(DebugThread), 0, DebugThread),
            ProvidedNativeFunction.Create(nameof(DebugAllVars), 0, DebugAllVars),
            ProvidedNativeFunction.Create(nameof(DebugAllThreads), 0, DebugAllThreads),
        };


        public DebuggingFunctionProvider(Interpreter interpreter)
            : base(interpreter)
        {
        }

        private static Union<InterpreterError, Variant> DebugVar(CallFrame frame, Variant[] args)
        {
            int indentation = args.Length < 2 ? 0 : (int)args[1].ToNumber();
            StringBuilder sb = new StringBuilder();

            if (args[0].AssignedTo is Variable var)
                sb.Append($@"${var.Name} : {{
    Value:         {var.Value.ToString().Trim()}
    Type:          {var.Value.Type}
    Raw Data:      ""{var.Value.RawData?.ToString().Trim()}"" ({var.Value.RawData?.GetType() ?? typeof(void)})
    Is Constant:   {var.IsConst}
    Is Global:     {var.IsGlobal}
    Decl.Location: {var.DeclaredLocation}
    Decl.Scope:    {var.DeclaredScope}
");
            else
                sb.Append($@"<unknown> : {{
    Value:         {args[0].ToString().Trim()}
    Type:          {args[0].Type}
    Raw Data:      ""{args[0].RawData?.ToString().Trim()}"" ({args[0].RawData?.GetType() ?? typeof(void)})
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

        private static Union<InterpreterError, Variant> DebugCallFrame(CallFrame frame, Variant[] args)
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

        private static Union<InterpreterError, Variant> DebugThread(CallFrame frame, Variant[] _)
        {
            StringBuilder sb = new StringBuilder();
            AU3Thread thread = frame.CurrentThread;

            sb.Append($@"Thread 0x{thread.ThreadID:x4} : {{
    ID:             {thread.ThreadID}
    Is Disposed:    {thread.IsDisposed}
    Is Main thread: {thread.IsMainThread}
    Is Running:     {thread.IsRunning}
    Callstack:      {0} Frames");
            CallFrame? cf = frame;
            int idx = 0;

            while (cf is { })
            {
                sb.Append($@"
        {++idx} : {{
            Location:  {cf}
            Function:  {cf.CurrentFunction}
            Arguments: {string.Join<Variant>(", ", cf.PassedArguments)}
        }}");

                cf = cf.CallerFrame;
            }

            sb.AppendLine("\n}");

            frame.Print(sb);

            return Variant.Null;
        }

        private static Union<InterpreterError, Variant> DebugAllVars(CallFrame frame, Variant[] _)
        {
            Union<InterpreterError, Variant> result;
            StringBuilder sb = new StringBuilder();
            List<VariableScope> scopes = new List<VariableScope> { frame.Interpreter.VariableResolver };
            int count;

            do
            {
                count = scopes.Count;

                foreach (VariableScope scope in from indexed in scopes.ToArray()
                                                from s in indexed.ChildScopes
                                                where !scopes.Contains(s)
                                                select s)
                    scopes.Add(scope);
            }
            while (count != scopes.Count);

            foreach (VariableScope scope in scopes)
            {
                sb.Append($@"{scope.InternalName} : {{
    Call frame:     {scope.CallFrame}
    Function:       {scope.CallFrame?.CurrentFunction}
    Is global:      {scope.IsGlobalScope}
    Parent scope:   {scope.Parent}
    Child sopes:    {scope.ChildScopes.Length}
");
                foreach (var child in scope.ChildScopes)
                    sb.AppendLine($"      - {child}");

                sb.AppendLine($"    Variable count: {scope.LocalVariables.Length}\n    Variables:");

                foreach (Variable global in scope.LocalVariables)
                {
                    result = DebugVar(frame, new Variant[] { global.Value, 2 });

                    if (result.Is<InterpreterError>())
                        return result;
                    else if (result.Is(out Variant value))
                        sb.AppendLine("      - " + value.ToString().Trim());
                }

                sb.AppendLine("}");
            }

            frame.Print(sb);

            return Variant.Zero;
        }

        private static Union<InterpreterError, Variant> DebugAllThreads(CallFrame frame, Variant[] _)
        {
            // TODO

            return Variant.Zero;
        }
    }
}
