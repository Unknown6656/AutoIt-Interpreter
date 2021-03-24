using System.Collections.Generic;
using System.Linq;
using System.Text;
using System;

using Unknown6656.AutoIt3.Extensibility.Plugins.Internals;
using Unknown6656.AutoIt3.Localization;
using Unknown6656.AutoIt3.Runtime;
using Unknown6656.AutoIt3.CLI;

using Unknown6656.Imaging;
using Unknown6656.Common;

namespace Unknown6656.AutoIt3.Extensibility.Plugins.Debugging
{
    public sealed class DebuggingFunctionProvider
        : AbstractFunctionProvider
    {
        public DebuggingFunctionProvider(Interpreter interpreter)
            : base(interpreter)
        {
            RegisterFunction(nameof(DebugVar), 1, DebugVar);
            RegisterFunction(nameof(DebugCallFrame), 0, DebugCallFrame);
            RegisterFunction(nameof(DebugThread), 0, DebugThread);
            RegisterFunction(nameof(DebugAllVars), 0, DebugAllVars);
            RegisterFunction(nameof(DebugAllCOM), 0, DebugAllCOM);
            RegisterFunction(nameof(DebugAllVarsCompact), 0, DebugAllVarsCompact);
            RegisterFunction(nameof(DebugCodeLines), 0, DebugCodeLines);
            RegisterFunction(nameof(DebugAllThreads), 0, DebugAllThreads);
            RegisterFunction(nameof(DebugInterpreter), 0, DebugInterpreter);
            RegisterFunction(nameof(DebugPlugins), 0, DebugPlugins);
            RegisterFunction(nameof(DebugAll), 0, DebugAll);
        }

        private IDictionary<string, object?> GetVariantInfo(Variant value)
        {
            IDictionary<string, object?> dic = new Dictionary<string, object?>
            {
                ["value"] = value.ToDebugString(Interpreter),
                ["type"] = value.Type,
                ["NETtype"] = value.RawType,
            };

            if (value.AssignedTo is Variable variable)
                dic["assignedTo"] = variable;

            if (value.ReferencedVariable is Variable @ref)
                dic["referenceTo"] = GetVariableInfo(@ref);

            return dic;
        }

        private IDictionary<string, object?> GetVariableInfo(Variable? variable) => new Dictionary<string, object?>
        {
            ["name"] = variable,
            ["constant"] = variable.IsConst,
            ["location"] = variable.DeclaredLocation,
            ["scope"] = variable.DeclaredScope,
            ["value"] = GetVariantInfo(variable.Value)
        };

        private IDictionary<string, object?> GetCallFrameInfo(CallFrame? frame)
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

        private IDictionary<string, object?> GetThreadInfo(AU3Thread thread) => new Dictionary<string, object?>
        {
            ["id"] = thread.ThreadID,
            ["disposed"] = thread.IsDisposed,
            ["isMain"] = thread.IsMainThread,
            ["running"] = thread.IsRunning,
            ["callstack"] = thread.CallStack.ToArray(GetCallFrameInfo)
        };

        private IDictionary<string, object?> GetAllVariables(Interpreter interpreter)
        {
            IDictionary<string, object?> dic = new Dictionary<string, object?>();
            List<VariableScope> scopes = new() { interpreter.VariableResolver };
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
            StringBuilder sb = new();
            string indent = $"{RGBAColor.DarkSlateGray.ToVT100ForegroundString()}│{MainProgram.COLOR_SCRIPT.ToVT100ForegroundString()}   ";

            sb.AppendLine(title + ": {");

            void serialize(IDictionary<string, object?> dic, int level)
            {
                int w = dic.Keys.Select(k => k.Length).Append(0).Max();

                foreach (string key in dic.Keys)
                {
                    if (level > 0)
                        sb.Append(Enumerable.Repeat(indent, level).StringConcat());

                    sb.Append((key + ':').PadRight(w + 1))
                      .Append(' ');

                    switch (dic[key])
                    {
                        case IDictionary<string, object?> d:
                            sb.AppendLine();
                            serialize(d, level + 1);

                            break;
                        case Array { Length: 0 }:
                            sb.Append("(0)");

                            break;
                        case Array arr:
                            sb.AppendLine($"({arr.Length})");

                            int index = 0;
                            int rad = 1 + (int)Math.Log10(arr.Length);

                            foreach (object? elem in arr)
                            {
                                sb.Append($"{Enumerable.Repeat(indent, level + 1).StringConcat()}[{index.ToString().PadLeft(rad, '0')}]: ");

                                if (elem is IDictionary<string, object?> d)
                                {
                                    sb.AppendLine();
                                    serialize(d, level + 2);
                                }
                                else
                                    sb.AppendLine(elem?.ToString());

                                ++index;
                            }

                            break;
                        case object obj:
                            sb.Append(obj);

                            break;
                    }

                    if (!sb.ToString().EndsWith(Environment.NewLine, StringComparison.InvariantCultureIgnoreCase))
                        sb.AppendLine();
                }
            }

            serialize(dic, 1);

            return sb.AppendLine("}")
                     .ToString();
        }

        private static FunctionReturnValue SerializePrint(CallFrame frame, IDictionary<string, object?> dic, object? title)
        {
            frame.Print(SerializeDictionary(dic, title is string s ? s : title?.ToString() ?? ""));

            return FunctionReturnValue.Success(Variant.Zero);
        }

        private string GenerateTable(IEnumerable<string?[]> cells, IEnumerable<(string header, bool align_right)> columns, int max_width, bool print_row_count, Predicate<int>? select = null) =>
            GenerateTable(cells.Transpose().Zip(columns).ToArray(t => (t.Second.Item1, t.Second.Item2, t.First)), max_width, print_row_count, select);

        private string GenerateTable((string header, bool align_right, string?[] cells)[] columns, int max_width, bool print_row_count, Predicate<int>? select = null)
        {
            StringBuilder sb = new();
            string?[,] data = new string?[columns.Length, columns.Max(col => col.cells.Length)];
            int[] widths = columns.ToArray(col => col.header.Length);

            for (int i = 0; i < widths.Length; i++)
            {
                string?[] cells = columns[i].cells;

                for (int j = 0; j < cells.Length; ++j)
                {
                    data[i, j] = cells[j];
                    widths[i] = Math.Max(widths[i], cells[j]?.Length ?? 0);
                }
            }

            max_width -= 1 + widths.Length;

            int r = 0;

            while (true)
            {
                int c_width = widths.Sum();
                int diff = c_width - max_width;

                if (diff > 0 && r <= widths.Length)
                {
                    (int w, int i) = widths.WithIndex().OrderByDescending(LINQ.fst).FirstOrDefault();

                    widths[i] = new[] { w - diff, w / 2, 3 }.Max();
                    ++r;
                }
                else
                    break;
            }

            if (print_row_count)
                sb.AppendLine(Interpreter.CurrentUILanguage["debug.rows", data.GetLength(1)]);

            sb.Append('┌');

            for (int i = 0, l = widths.Length; i < l; i++)
                sb.Append(new string('─', widths[i]))
                  .Append(i < l - 1 ? '┬' : '┐');

            sb.AppendLine()
              .Append('│');

            for (int i = 0; i < widths.Length; i++)
                if (columns[i].header.Length > widths[i])
                    sb.Append(columns[i].header[..(widths[i] - 3)])
                      .Append("...");
                else
                    sb.Append(columns[i].align_right ? columns[i].header.PadLeft(widths[i]) : columns[i].header.PadRight(widths[i]))
                      .Append('│');

            sb.AppendLine().Append('├');

            for (int i = 0, l = widths.Length; i < l; i++)
                sb.Append(new string('─', widths[i]))
                  .Append(i < l - 1 ? '┼' : '┤');

            sb.AppendLine();

            for (int j = 0, l = data.GetLength(1); j < l; ++j)
            {
                bool sel = select?.Invoke(j) ?? false;

                if (sel)
                    sb.Append("\x1b[7m");

                for (int i = 0; i < widths.Length; i++)
                {
                    sb.Append('│');

                    string val = data[i, j] ?? "";

                    if (val.Length > widths[i])
                        sb.Append(val[..(widths[i] - 3)])
                          .Append("...");
                    else
                        sb.Append(columns[i].align_right ? val.PadLeft(widths[i]) : val.PadRight(widths[i]));
                }

                sb.Append('│');

                if (sel)
                    sb.Append("\x1b[27m");

                sb.AppendLine();
            }

            sb.Append('└');

            for (int i = 0, l = widths.Length; i < l; i++)
                sb.Append(new string('─', widths[i]))
                  .Append(i < l - 1 ? '┴' : '┘');

            return sb.AppendLine()
                     .ToString();
        }

        public FunctionReturnValue DebugVar(CallFrame frame, Variant[] args) => SerializePrint(frame, GetVariableInfo(args[0].AssignedTo), args[0].AssignedTo);

        public FunctionReturnValue DebugCallFrame(CallFrame frame, Variant[] _) => SerializePrint(frame, GetCallFrameInfo(frame), "Call Frame");

        public FunctionReturnValue DebugThread(CallFrame frame, Variant[] _) => SerializePrint(frame, GetThreadInfo(frame.CurrentThread), frame.CurrentThread);

        public FunctionReturnValue DebugAllVars(CallFrame frame, Variant[] _) => SerializePrint(frame, GetAllVariables(frame.Interpreter), frame.Interpreter);

        public FunctionReturnValue DebugAllVarsCompact(CallFrame frame, Variant[] _)
        {
            List<VariableScope> scopes = new() { frame.Interpreter.VariableResolver };
            LanguagePack lang = Interpreter.CurrentUILanguage;
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

            object? netobj = null;
            StringBuilder sb = new();
            var iterators = from kvp in InternalsFunctionProvider._iterators
                            let index = kvp.Value.index
                            let tuple = kvp.Value.index < kvp.Value.collection.Length ? kvp.Value.collection[kvp.Value.index] : default
                            select (
                                $"/iter/{kvp.Key}",
                                MainProgram.ASM_FILE.Name,
                                lang["debug.iterator"],
                                $"{lang["debug.index"]}:{index}, {lang["debug.length"]}:{kvp.Value.collection.Length}, {lang["debug.key"]}:{tuple.key.ToDebugString(Interpreter)}, {lang["debug.value"]}:{tuple.value.ToDebugString(Interpreter)}",
                                  "",
                                  "ITERATOR"
                            );
            var global_objs = from id in frame.Interpreter.GlobalObjectStorage.HandlesInUse
                              where frame.Interpreter.GlobalObjectStorage.TryGet(id, out netobj)
                              select (
                                  $"/obj/{(uint)id:x8}",
                                  MainProgram.ASM_FILE.Name,
                                  lang["debug.netobj"],
                                  netobj?.ToString() ?? "<null>",
                                  "",
                                  ".NET"
                              );
            var au3_vars = from scope in scopes
                           from variable in scope.LocalVariables
                           let name = scope.InternalName + '$' + variable.Name
                           orderby name ascending
                           select (
                               name,
                               variable.DeclaredLocation.ToString(),
                               variable.Value.Type.ToString(),
                               variable.Value.ToDebugString(Interpreter),
                               variable.ReferencedVariable?.ToString() ?? "",
                               (variable.IsConst ? "CONST" : "") + 
                               (variable.IsGlobal ? " GLOBAL" : "")
                           );

            (string name, string loc, string type, string value, string ref_to, string modifiers)[] variables = new[] { au3_vars, iterators, global_objs }.SelectMany(LINQ.id).ToArray();
            Array.Sort(variables, (x, y) =>
            {
                string[] pathx = x.name.Split('/');
                string[] pathy = y.name.Split('/');

                for (int i = 0, l = Math.Min(pathx.Length, pathy.Length); i < l; ++i)
                {
                    bool varx = pathx[i].StartsWith('$');
                    int cmp = varx ^ pathy[i].StartsWith('$') ? varx ? -1 : 1 : string.Compare(pathx[i], pathy[i], StringComparison.InvariantCultureIgnoreCase);

                    if (cmp != 0)
                        return cmp;
                }

                return string.Compare(x.name, y.name, StringComparison.InvariantCultureIgnoreCase);
            });

            string table = GenerateTable(
                variables.Select(row => new string?[] { row.name, row.loc, row.type, row.ref_to, row.modifiers, row.value }),
                new[] {
                    (lang["debug.name"], false),
                    (lang["debug.location"], false),
                    (lang["debug.type"], false),
                    (lang["debug.reference_to"], true),
                    (lang["debug.modifiers"], true),
                    (lang["debug.value"], true),
                },
                Math.Min(Console.BufferWidth, Console.WindowWidth),
                true
            );

            frame.Print(table);

            return Variant.Zero;
        }

        public FunctionReturnValue DebugAllCOM(CallFrame frame, Variant[] _)
        {
            if (Interpreter.COMConnector?.GetAllCOMObjectInfos() is { } objects)
                frame.Print(GenerateTable(
                    objects.Select(t => new string?[]
                    {
                        $"/com/{t.id:x8}",
                        t.type,
                        t.clsid,
                        t.value.ToDebugString(Interpreter),
                    }),
                    new[] {
                        (Interpreter.CurrentUILanguage["debug.object"], false),
                        (Interpreter.CurrentUILanguage["debug.type"], false),
                        ("CLSID", false),
                        (Interpreter.CurrentUILanguage["debug.value"], true),
                    },
                    Math.Min(Console.BufferWidth, Console.WindowWidth),
                    true
                ));

            return Variant.Zero;
        }

        public FunctionReturnValue DebugCodeLines(CallFrame frame, Variant[] _)
        {
            if (frame.CurrentThread.CallStack.OfType<AU3CallFrame>().FirstOrDefault() is AU3CallFrame au3frame)
            {
                StringBuilder sb = new();
                (SourceLocation loc, string txt)[] lines = au3frame.CurrentLineCache;
                int eip = au3frame.CurrentInstructionPointer;

                string table = GenerateTable(new[]
                {
                    ("", true, Enumerable.Range(0, lines.Length).ToArray(i => i.ToString())),
                    (Interpreter.CurrentUILanguage["debug.location"], false, lines.ToArray(t => t.loc.ToString())),
                    (Interpreter.CurrentUILanguage["debug.content"], false, lines.ToArray(LINQ.snd)),
                }, Math.Min(Console.BufferWidth, Console.WindowWidth), true, i => i == eip);

                frame.Print(table);
            }

            return Variant.Zero;
        }

        public FunctionReturnValue DebugAllThreads(CallFrame frame, Variant[] _)
        {
            AU3Thread[] threads = frame.Interpreter.Threads.Where(t => !t.IsDisposed).OrderBy(t => t.ThreadID).ToArray();
            StringBuilder sb = new();

            sb.AppendLine($"Overview ({threads.Length} threads):");

            foreach (var ts in threads.Select(t => ($"Thread {t.ThreadID}{(t.IsMainThread ? " (main)" : t.IsRunning ? " (active)" : "")}", true, t.CallStack.ToArray(f => f.CurrentFunction.Name)))
                                      .PartitionByArraySize(6))
                sb.Append(GenerateTable(ts!, Math.Min(Console.BufferWidth, Console.WindowWidth), false));

            sb.AppendLine();

            foreach (AU3Thread thread in threads)
                sb.Append(SerializeDictionary(GetThreadInfo(thread), $"Thread {thread.ThreadID}"));

            frame.Print(sb.ToString());

            return Variant.Zero;
        }

        public FunctionReturnValue DebugInterpreter(CallFrame frame, Variant[] _)
        {
            Interpreter interpreter = frame.Interpreter;
            Dictionary<string, object?> dic = new()
            {
                ["CurrentLang"] = interpreter.CurrentUILanguage,
                ["LoadedLangs"] = interpreter.LanguageLoader.LoadedLanguageCodes,
                ["Threads"] = interpreter.Threads,
                ["Scripts"] = interpreter.ScriptScanner.ActiveScripts,
                ["Plugins"] = interpreter.PluginLoader.LoadedPlugins,
                ["CommandLine"] = interpreter.CommandLineOptions,
                ["GlobalObjects"] = interpreter.GlobalObjectStorage.Objects.ToArray(),
                ["GlobalVariables"] = interpreter.VariableResolver.LocalVariables,
            };

            frame.Print(SerializeDictionary(dic, "Interpreter"));

            return Variant.Zero;
        }

        public FunctionReturnValue DebugPlugins(CallFrame frame, Variant[] _)
        {
            LanguagePack lang = Interpreter.CurrentUILanguage;
            PluginLoader loader = frame.Interpreter.PluginLoader;

            string table = GenerateTable(
                loader.LoadedPlugins.Select(plugin => new string?[] { plugin.TypeName, plugin.PluginCategory.ToString(), plugin.Location.FullName }),
                new[] {
                    (lang["debug.name"], false),
                    (lang["debug.type"], false),
                    (lang["debug.location"], false),
                },
                Math.Min(Console.BufferWidth, Console.WindowWidth),
                true
            );

            // TODO : detailed report on each plugin or plugin category

            frame.Print(table);

            return FunctionReturnValue.Success(Variant.Zero);
        }

        public FunctionReturnValue DebugAll(CallFrame frame, Variant[] args)
        {
            DebugCodeLines(frame, args);
            // DebugCallFrame(frame, args);
            DebugAllVarsCompact(frame, args);
            DebugAllCOM(frame, args);
            DebugAllThreads(frame, args);
            DebugInterpreter(frame, args);

            return Variant.Zero;
        }
    }
}
