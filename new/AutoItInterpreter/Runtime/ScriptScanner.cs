using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

using Unknown6656.AutoIt3.Extensibility;
using Unknown6656.Common;
using Unknown6656.IO;

namespace Unknown6656.AutoIt3.Runtime
{
    public sealed class ScriptScanner
    {
        private readonly ConcurrentDictionary<string, ScannedScript> _cached_scripts = new ConcurrentDictionary<string, ScannedScript>();


        public Interpreter Interpreter { get; }


        public ScriptScanner(Interpreter interpreter) => Interpreter = interpreter;

        public Union<InterpreterError, (FileInfo physical_file, string content)> ResolveScriptFile(SourceLocation include_loc, string path)
        {
            // TODO : built-in

            foreach (AbstractIncludeResolver res in Interpreter.PluginLoader.IncludeResolvers)
                if (res.TryResolve(path, out (FileInfo fi, string ct)? file))
                    return file;

            return InterpreterError.WellKnown(include_loc, "error.unresolved_script", path);
        }

        public Union<InterpreterError, ScannedScript> ScanScriptFile(SourceLocation include_loc, string path) =>
            ResolveScriptFile(include_loc, path).Match<Union<InterpreterError, ScannedScript>>(err => err, file =>
            {
                string key = file.physical_file.FullName;

                if (!_cached_scripts.TryGetValue(key, out ScannedScript? script))
                {
                    script = new ScannedScript(file.physical_file);

                    ScannedFunction curr_func = script.GetOrCreateFunction(ScannedFunction.GLOBAL_FUNC);
                    string[] lines = From.String(file.content).To.Lines();
                    int comment_lvl = 0;
                    Match m;

                    for (int i = 0; i < lines.Length; ++i)
                    {
                        SourceLocation loc = new SourceLocation(file.physical_file, i);
                        string line = TrimComment(lines[i].TrimStart());
                        bool handled = line.Match(
                            (@"^#(comments\-start|cs)(\b|$)", _ => ++comment_lvl),
                            (@"^#(comments\-end|ce)(\b|$)", _ => comment_lvl = Math.Max(0, comment_lvl - 1)),
                            (@"^#(end-?)?region\b", delegate { }
                        )
                        );

                        if (handled || comment_lvl > 0)
                            continue;

                        if (line.Match(
                            (@"^#(onautoitstartregister\s+""(?<func>[^""]+)"")", m => script.AddStartupFunction(m.Groups["func"].Value, loc)),
                            (@"^#(onautoitexitregister\s+""(?<func>[^""]+)"")", m => script.AddExitFunction(m.Groups["func"].Value, loc))
                        ))
                            continue;
                        else if (line.Match(@"^#requireadmin\b", out m))
                            line = "#pragma compile(ExecLevel, requireAdministrator)";
                        else if (line.Match(@"^#notrayicon\b", out m))
                            line = @"Opt(""TrayIconHide"", 1)";

                        if (line.Match(@"^#pragma\s+(?<option>[a-z_]\w+)\b\s*(\((?<params>.*)\))?\s*", out m))
                        {
                            string option = m.Groups["option"].Value.Trim();
                            string @params = m.Groups["params"].Value;
                            string? value = null;

                            if (@params.IndexOf(',') is int idx && idx > 0)
                            {
                                @params = @params[..idx].ToLower().Trim();
                                value = @params[(idx + 1)..].Trim();
                            }

                            if (ProcessPragma(loc, option.ToLower(), @params, value) is InterpreterError err)
                                return err;
                            else
                                continue;
                        }

                        while (line.Match(@"(\s|^)_$", out m))
                            if (i == lines.Length - 1)
                                return InterpreterError.WellKnown(loc, "error.unexpected_line_cont");
                            else
                            {
                                line = line[..m.Index] + TrimComment(lines[++i].TrimStart());
                                loc = new SourceLocation(loc.FileName, loc.StartLineNumber, i);
                            }

                        if (line.Match(@"^func\s+(?<name>[a-z_]\w*)\s*\(.*\)\s*$", out m))
                        {
                            string name = m.Groups["name"].Value;

                            if (!curr_func.IsMainFunction)
                                return InterpreterError.WellKnown(loc, "error.unexpected_func", curr_func.Name);
                            else if (script.HasFunction(name))
                            {
                                ScannedFunction existing = script.GetOrCreateFunction(name);

                                return InterpreterError.WellKnown(loc, "error.duplicate_function", existing.Name, existing.Location);
                            }

                            curr_func = script.GetOrCreateFunction(name);
                        }
                        else if (line.Match(@"^endfunc$", out Match _))
                        {
                            if (curr_func.IsMainFunction)
                                return InterpreterError.WellKnown(loc, "error.unexpected_endfunc");

                            curr_func = script.MainFunction;
                        }
                        else if (!string.IsNullOrWhiteSpace(line))
                            curr_func.AddLine(loc, line);
                    }

                    _cached_scripts.TryAdd(key, script);
                }

                return script;
            });

        private InterpreterError? ProcessPragma(SourceLocation loc, string option, string key, string? value)
        {
            if (option == "compile")
            {
                switch (key)
                {
                    case "out": break;
                    case "icon": break;
                    case "execlevel": break;
                    case "upx": break;
                    case "autoitexecuteallowed": break;
                    case "console": break;
                    case "compression": break;
                    case "compatibility": break;
                    case "x64": break;
                    case "inputboxres": break;
                    case "comments": break;
                    case "companyname": break;
                    case "filedescription": break;
                    case "fileversion": break;
                    case "internalname": break;
                    case "legalcopyright": break;
                    case "legaltrademarks": break;
                    case "originalfilename": break;
                    case "productname": break;
                    case "productversion": break;
                    default:
                        return InterpreterError.WellKnown(loc, "error.unhandled_pragma_key", key, option);
                }

                return InterpreterError.WellKnown(loc, "error.not_yet_implemented", "compile");
            }

            foreach (AbstractPragmaProcessor proc in Interpreter.PluginLoader.PragmaProcessors)
                if (proc.PragmaName.Equals(option, StringComparison.InvariantCultureIgnoreCase))
                    if (proc.CanProcessPragmaKey(key))
                        return proc.ProcessPragma(loc, key, value);
                    else
                        return InterpreterError.WellKnown(loc, "error.unhandled_pragma_key", key, option);

            return InterpreterError.WellKnown(loc, "error.unhandled_pragma_option", option);
        }

        private static string TrimComment(string? line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return "";
            else if (line.Contains(';'))
                if (line.Match(@"\;[^\""\']*$", out Match m))
                    line = line[..m.Index];
                else
                {
                    string before = line[..line.IndexOf(';')];

                    if (!line.Contains("$\"") && ((before.CountOccurences("\"") % 2) == 0) && (before.CountOccurences("'") % 2) == 0)
                        line = before.Trim();
                    else if (line.Match(@"^([^\""\;]*\""[^\""]*\""[^\""\;]*)*(?<cmt>\;).*$", out m))
                        line = line[..m.Groups["cmt"].Index];
                    else if (line.Match(@"^([^'\;]*'[^']*'[^'\;]*)*(?<cmt>\;).*$", out m))
                        line = line[..m.Groups["cmt"].Index];
                }

            return line.TrimEnd();
        }
    }

    public sealed class ScannedScript
        : IEquatable<ScannedScript>
    {
        private readonly Dictionary<string, ScannedFunction> _functions = new Dictionary<string, ScannedFunction>();
        private readonly List<(string func, SourceLocation decl)> _startup = new List<(string, SourceLocation)>();
        private readonly List<(string func, SourceLocation decl)> _exit = new List<(string, SourceLocation)>();


        public ImmutableDictionary<string, ScannedFunction> Functions => _functions.ToImmutableDictionary();

        public ScannedFunction MainFunction => _functions[ScannedFunction.GLOBAL_FUNC];

        public ScannedScriptState State { get; private set; } = ScannedScriptState.Unloaded;

        public bool IsLoaded => State == ScannedScriptState.Loaded;

        public FileInfo Location { get; }


        public ScannedScript(FileInfo location) => Location = location;

        internal ScannedFunction GetOrCreateFunction(string name) =>
            _functions.TryGetValue(name.ToLower(), out ScannedFunction? func) ? func : AddFunction(new ScannedFunction(this, name));

        internal ScannedFunction AddFunction(ScannedFunction function) => _functions[function.Name.ToLower()] = function;

        internal void AddStartupFunction(string name, SourceLocation decl) => _startup.Add((name.ToLower(), decl));

        internal void AddExitFunction(string name, SourceLocation decl) => _exit.Add((name.ToLower(), decl));

        public InterpreterResult LoadScript(CallFrame frame)
        {
            if (State == ScannedScriptState.Loaded)
                return InterpreterResult.OK;

            State = ScannedScriptState.Loaded;

            InterpreterResult? result = null;

            foreach ((string name, SourceLocation loc) in _startup)
                if (_functions.TryGetValue(name, out ScannedFunction? func))
                {
                    if (result?.IsOK ?? false)
                        result = null;

                    result ??= frame.Call(func);
                }
                else
                    return InterpreterError.WellKnown(loc, "error.unresolved_func", name);

            return result ?? InterpreterResult.OK;
        }

        public InterpreterResult UnLoadScript(CallFrame frame)
        {
            if (State == ScannedScriptState.Unloaded)
                return InterpreterResult.OK;

            State = ScannedScriptState.Unloaded;

            InterpreterResult? result = null;

            foreach ((string name, SourceLocation loc) in _exit)
                if (_functions.TryGetValue(name, out ScannedFunction? func))
                {
                    if (result?.IsOK ?? false)
                        result = null;

                    result ??= frame.Call(func);
                }
                else
                    return InterpreterError.WellKnown(loc, "error.unresolved_func", name);

            return result ?? InterpreterResult.OK;
        }

        public override int GetHashCode() => Location.FullName.GetHashCode();

        public override bool Equals(object? obj) => Equals(obj as ScannedScript);

        public override string ToString() => Location.ToString();

        public bool Equals(ScannedScript? other) => other is ScannedScript script && GetHashCode() == script.GetHashCode();

        public bool HasFunction(string name) => _functions.ContainsKey(name.ToLower());


        public static bool operator ==(ScannedScript? s1, ScannedScript? s2) => s1?.Equals(s2) ?? s2 is null;

        public static bool operator !=(ScannedScript? s1, ScannedScript? s2) => !(s1 == s2);
    }

    public sealed class ScannedFunction
        : IEquatable<ScannedFunction>
    {
        internal const string GLOBAL_FUNC = "$global";

        private readonly ConcurrentDictionary<SourceLocation, string> _lines = new ConcurrentDictionary<SourceLocation, string>();


        public string Name { get; }

        public ScannedScript Script { get; }

        public SourceLocation Location
        {
            get
            {
                SourceLocation[] lines = _lines.Keys.OrderBy(Generics.id).ToArray();

                return new SourceLocation(lines[0].FileName, lines[0].StartLineNumber, lines[^1].EndLineNumber);
            }
        }


        public bool IsMainFunction => Name.Equals(GLOBAL_FUNC, StringComparison.InvariantCultureIgnoreCase);

        public (SourceLocation LineLocation, string LineContent)[] Lines => _lines.OrderBy(k => k.Key).ToArray(k => (k.Key, k.Value));

        public int LinesCount => _lines.Count;


        internal ScannedFunction(ScannedScript script, string name)
        {
            Name = name;
            Script = script;
            Script.AddFunction(this);
        }

        public void AddLine(SourceLocation location, string content) => _lines.AddOrUpdate(location, content, (l, c) => content);

        public override int GetHashCode() => HashCode.Combine(Name.ToLower(), Script);

        public override bool Equals(object? obj) => Equals(obj as ScannedFunction);

        public bool Equals(ScannedFunction? other) => other is ScannedFunction f && f.GetHashCode() == GetHashCode();

        public override string ToString() => $"[{Script}] Func {Name}(...)  ({_lines.Count} Lines)";


        public static bool operator ==(ScannedFunction? s1, ScannedFunction? s2) => s1?.Equals(s2) ?? s2 is null;

        public static bool operator !=(ScannedFunction? s1, ScannedFunction? s2) => !(s1 == s2);
    }

    public enum ScannedScriptState
    {
        Unloaded,
        Loaded
    }
}
