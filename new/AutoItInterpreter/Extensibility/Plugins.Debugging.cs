using System.Collections.Generic;
using System.Linq;
using System.Text;
using System;

using Unknown6656.AutoIt3.Runtime;
using Unknown6656.Common;

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
            ProvidedNativeFunction.Create(nameof(DebugAllVarsCompact), 0, DebugAllVarsCompact),
            ProvidedNativeFunction.Create(nameof(DebugAllThreads), 0, DebugAllThreads),
        };


        public DebuggingFunctionProvider(Interpreter interpreter)
            : base(interpreter)
        {
        }

        private static IDictionary<string, object?> GetVariantInfo(Variant value)
        {
            IDictionary<string, object?> dic = new Dictionary<string, object?>
            {
                ["value"] = value.ToString().Trim(),
                ["type"] = value.Type,
                ["raw"] = $"\"{value.RawData?.ToString()?.Trim()}\" ({value.RawData?.GetType() ?? typeof(void)})"
            };

            if (value.AssignedTo is Variable variable)
                dic["assignedTo"] = variable;

            if (value.ReferencedVariable is Variable @ref)
                dic["referenceTo"] = GetVariableInfo(@ref);

            return dic;
        }

        private static IDictionary<string, object?> GetVariableInfo(Variable variable) => new Dictionary<string, object?>
        {
            ["name"] = variable,
            ["constant"] = variable.IsConst,
            ["global"] = variable.IsGlobal,
            ["location"] = variable.DeclaredLocation,
            ["scope"] = variable.DeclaredScope,
            ["value"] = GetVariantInfo(variable.Value)
        };

        private static IDictionary<string, object?> GetCallFrameInfo(CallFrame? frame)
        {
            IDictionary<string, object?> dic = new Dictionary<string, object?>();

            frame = frame?.CallerFrame;

            if (frame is { })
            {
                dic["type"] = frame.GetType().Name;
                dic["thread"] = frame.CurrentThread;
                dic["function"] = frame.CurrentFunction;
                dic["ret.value"] = frame.ReturnValue;
                dic["variables"] = frame.VariableResolver.LocalVariables.ToArray(GetVariableInfo);
                dic["arguments"] = frame.PassedArguments.ToArray(GetVariantInfo);

                if (frame is AU3CallFrame au3)
                {
                    dic["location"] = au3.CurrentLocation;
                    dic["line"] = $"\"{au3.CurrentLineContent}\"";
                }
            }

            return dic;
        }

        private static IDictionary<string, object?> GetThreadInfo(AU3Thread thread) => new Dictionary<string, object?>
        {
            ["id"] = thread.ThreadID,
            ["disposed"] = thread.IsDisposed,
            ["isMain"] = thread.IsMainThread,
            ["running"] = thread.IsRunning,
            ["callstack"] = thread.CallStack.ToArray(GetCallFrameInfo)
        };

        private static IDictionary<string, object?> GetAllVariables(Interpreter interpreter)
        {
            IDictionary<string, object?> dic = new Dictionary<string, object?>();
            List<VariableScope> scopes = new List<VariableScope> { interpreter.VariableResolver };
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
                dic[scope.InternalName] = new Dictionary<string, object?>
                {
                    ["frame"] = scope.CallFrame,
                    ["function"] = scope.CallFrame?.CurrentFunction,
                    ["isGlobal"] = scope.IsGlobalScope,
                    ["parent"] = scope.Parent,
                    ["children"] = scope.ChildScopes.ToArray(c => c.InternalName),
                    ["variables"] = scope.LocalVariables.ToArray(GetVariableInfo),
                };

            return dic;
        }

        private static string SerializeDictionary(IDictionary<string, object?> dic, string title)
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine(title + ": {");

            void serialize(IDictionary<string, object?> dic, int level)
            {
                int w = dic.Keys.Select(k => k.Length).Append(0).Max();

                foreach (string key in dic.Keys)
                {
                    sb.Append($"{new string(' ', level * 4)}{(key + ':').PadRight(w + 1)} ");

                    switch (dic[key])
                    {
                        case IDictionary<string, object?> d:
                            sb.AppendLine();
                            serialize(d, level + 1);

                            break;
                        case Array { Length: 0 }:
                            sb.Append($"(0)");

                            break;
                        case Array arr:
                            sb.AppendLine($"({arr.Length})");

                            int index = 0;
                            int rad = 1 + (int)Math.Log10(arr.Length);

                            foreach (object? elem in arr)
                            {
                                sb.Append($"{new string(' ', (level + 1) * 4)}[{index.ToString().PadLeft(rad, '0')}]: ");

                                if (elem is IDictionary<string, object?> d)
                                {
                                    sb.AppendLine();
                                    serialize(d, level + 2);
                                }
                                else
                                    sb.Append(elem?.ToString());

                                ++index;
                            }

                            break;
                        case object obj:
                            sb.Append(obj);

                            break;
                    }

                    if (!sb.ToString().EndsWith(Environment.NewLine))
                        sb.AppendLine();
                }
            }

            serialize(dic, 1);

            return sb.AppendLine("}")
                     .ToString();
        }

        private static Variant SerializePrint(CallFrame frame, IDictionary<string, object?> dic, object? title)
        {
            frame.Print(SerializeDictionary(dic, title is string s ? s : title?.ToString() ?? ""));

            return Variant.Zero;
        }

        private static Union<InterpreterError, Variant> DebugVar(CallFrame frame, Variant[] args) => SerializePrint(frame, GetVariableInfo(args[0].AssignedTo), args[0].AssignedTo);

        private static Union<InterpreterError, Variant> DebugCallFrame(CallFrame frame, Variant[] args) => SerializePrint(frame, GetCallFrameInfo(frame), "Call Frame");

        private static Union<InterpreterError, Variant> DebugThread(CallFrame frame, Variant[] _) => SerializePrint(frame, GetThreadInfo(frame.CurrentThread), frame.CurrentThread);

        private static Union<InterpreterError, Variant> DebugAllVars(CallFrame frame, Variant[] _) => SerializePrint(frame, GetAllVariables(frame.Interpreter), frame.Interpreter);

        private static Union<InterpreterError, Variant> DebugAllVarsCompact(CallFrame frame, Variant[] _)
        {
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

            StringBuilder sb = new StringBuilder();
            (string name, string type, string value)[]? variables = (from scope in scopes
                                                                     from variable in scope.LocalVariables
                                                                     let name = scope.InternalName + '$' + variable.Name
                                                                     orderby name ascending
                                                                     select (name, variable.Value.Type.ToString(), variable.Value.ToString())).ToArray();
            int w_name = variables.Select(t => t.name.Length).Append(4).Max();
            int w_type = variables.Select(t => t.type.Length).Append(4).Max();
            int w_value = variables.Select(t => t.value.Length).Append(5).Max();

            w_value = Math.Min(w_value + 3, Console.BufferWidth - 12 - w_type - w_name);

            sb.Append($@"{variables.Length} Variables:
| {"Name".PadRight(w_name)} | {"Type".PadRight(w_type)} | {"Value".PadRight(w_value)} |
|{new string('-', w_name + 2)}+{new string('-', w_type + 2)}+{new string('-', w_value + 2)}|
");

            foreach ((string name, string type, string value) in variables)
            {
                string val = value.Length > w_value - 3 ? value[..^3] + "..."  : value.PadLeft(w_value);

                sb.AppendLine($"| {name.PadRight(w_name)} | {type.PadRight(w_type)} | {val} |");
            }

            frame.Print(sb.ToString());

            return Variant.Zero;
        }

        private static Union<InterpreterError, Variant> DebugAllThreads(CallFrame frame, Variant[] _)
        {
            // TODO

            return Variant.Zero;
        }
    }
}
